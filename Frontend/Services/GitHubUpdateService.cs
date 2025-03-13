using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    internal class GitHubUpdateService : IUpdateService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _updateDirectory;
        private readonly string _backupDirectory;
        private readonly AppSettings _settings;
        private Models.UpdateInfo? _latestVersion;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _currentVersion;
        private readonly string _userAgent;
        private bool _disposed = false;
        private readonly Collection<GitHubAsset> _assets = new Collection<GitHubAsset>();
        public IReadOnlyCollection<GitHubAsset> Assets => _assets;

        public event EventHandler<UpdateProgressEventArgs>? UpdateProgress;
        public event EventHandler<UpdateStatusEventArgs>? UpdateStatusChanged;

        public GitHubUpdateService(
            IHttpClientFactory httpClientFactory,
            IOptions<AppSettings> settings,
            IOptions<UpdateSettings> updateSettings)
        {
            if (httpClientFactory == null) throw new ArgumentNullException(nameof(httpClientFactory));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (updateSettings == null) throw new ArgumentNullException(nameof(updateSettings));

            _httpClient = httpClientFactory.CreateClient();
            _settings = settings.Value;
            _logger = Log.ForContext<GitHubUpdateService>();

            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArmaReforgerServerMonitor");

            _updateDirectory = Path.Combine(baseDir, "Updates");
            _backupDirectory = Path.Combine(baseDir, "Backups");

            Directory.CreateDirectory(_updateDirectory);
            Directory.CreateDirectory(_backupDirectory);

            _owner = updateSettings.Value.GitHubOwner ?? throw new ArgumentNullException(nameof(updateSettings.Value.GitHubOwner));
            _repo = updateSettings.Value.GitHubRepo ?? throw new ArgumentNullException(nameof(updateSettings.Value.GitHubRepo));
            _currentVersion = updateSettings.Value.CurrentVersion ?? throw new ArgumentNullException(nameof(updateSettings.Value.CurrentVersion));
            _userAgent = $"ArmaReforgerServerMonitor/{_currentVersion}";
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                _logger.Information("Checking for updates...");
                var releases = await GetReleasesAsync().ConfigureAwait(false);
                var latestRelease = releases.FirstOrDefault(r => !r.Draft && !r.Prerelease);

                if (latestRelease == null)
                {
                    _logger.Information("No stable releases found");
                    OnUpdateStatusChanged(UpdateStatus.NoUpdateAvailable);
                    return false;
                }

                var currentVersion = Version.Parse(_currentVersion);
                var latestVersion = Version.Parse(latestRelease.TagName.TrimStart('v'));

                var hasUpdate = latestVersion > currentVersion;
                _logger.Information("Update check complete. Has update: {HasUpdate}", hasUpdate);

                OnUpdateStatusChanged(hasUpdate ? UpdateStatus.UpdateAvailable : UpdateStatus.NoUpdateAvailable);
                return hasUpdate;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request error while checking for updates");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "JSON parsing error while checking for updates");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while checking for updates");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
        }

        public async Task<Models.UpdateInfo?> GetLatestVersionInfoAsync()
        {
            try
            {
                var apiUrl = new Uri(_settings.Updates.GithubRepositoryUrl + "/releases/latest");
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(apiUrl).ConfigureAwait(false);

                if (release == null)
                {
                    _logger.Warning("Failed to get release information from GitHub");
                    return null;
                }

                var version = new Version(release.TagName.TrimStart('v'));
                var downloadUrl = new Uri(release.Assets[0].BrowserDownloadUrl);
                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var downloadSize = response.Content.Headers.ContentLength ?? 0;

                _latestVersion = new Models.UpdateInfo(
                    version.ToString(),
                    release.Body,
                    downloadSize,
                    release.PublishedAt,
                    release.Prerelease,
                    new Uri(downloadUrl.ToString()));

                return _latestVersion ?? throw new InvalidOperationException("Latest version information is not available.");
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Failed to get latest version info");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "JSON parsing error while getting latest version info");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while getting latest version info");
                return null;
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(bool silent = false)
        {
            try
            {
                if (_latestVersion == null)
                {
                    _logger.Warning("No update information available");
                    return false;
                }

                OnUpdateStatusChanged(UpdateStatus.Downloading);
                var updateFile = Path.Combine(_updateDirectory, "update.zip");

                HttpResponseMessage? response = null;
                Stream? contentStream = null;
                FileStream? fileStream = null;

                try
                {
                    response = await _httpClient.GetAsync(_latestVersion.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var totalBytesRead = 0L;

                    contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    fileStream = File.Create(updateFile);
                    var buffer = new byte[8192];
                    var isMoreToRead = true;

                    do
                    {
                        var bytesRead = await contentStream.ReadAsync(buffer).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            isMoreToRead = false;
                            continue;
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        totalBytesRead += bytesRead;

                        if (totalBytes != -1L)
                        {
                            var progressPercentage = (int)((double)totalBytesRead / totalBytes * 100);
                            OnUpdateProgress("Downloading", progressPercentage);
                        }
                    }
                    while (isMoreToRead);
                }
                finally
                {
                    response?.Dispose();
                    contentStream?.Dispose();
                    fileStream?.Dispose();
                }

                // Create backup before installing
                await CreateBackupAsync().ConfigureAwait(false);

                OnUpdateStatusChanged(UpdateStatus.Installing);
                if (await InstallUpdateAsync(updateFile, silent).ConfigureAwait(false))
                {
                    OnUpdateStatusChanged(UpdateStatus.Complete);
                    return true;
                }

                OnUpdateStatusChanged(UpdateStatus.Failed, "Installation failed");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request error during update download");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "IO error during update installation");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, "Unauthorized access during update installation");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during update download and installation");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
        }

        public async Task<bool> RollbackAsync()
        {
            try
            {
                OnUpdateStatusChanged(UpdateStatus.RollingBack);
                var backupFile = Directory.GetFiles(_backupDirectory, "*.zip").OrderByDescending(f => File.GetCreationTime(f)).FirstOrDefault();

                if (backupFile == null)
                {
                    _logger.Warning("No backup found for rollback");
                    OnUpdateStatusChanged(UpdateStatus.Failed, "No backup available");
                    return false;
                }

                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var tempDir = Path.Combine(_backupDirectory, "temp");
                Directory.CreateDirectory(tempDir);

                // Extract backup to temp directory
                OnUpdateProgress("Extracting backup", 0);
                await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(backupFile, tempDir, true)).ConfigureAwait(false);
                OnUpdateProgress("Extracting backup", 50);

                // Copy files back
                foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(tempDir, file);
                    var targetPath = Path.Combine(currentDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Copy(file, targetPath, true);
                }

                OnUpdateProgress("Extracting backup", 100);
                Directory.Delete(tempDir, true);
                OnUpdateStatusChanged(UpdateStatus.Complete);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to rollback update");
                OnUpdateStatusChanged(UpdateStatus.Failed, ex.Message);
                return false;
            }
        }

        public VersionInfo GetCurrentVersion()
        {
            var assembly = GetType().Assembly;
            var version = assembly.GetName().Version ?? new Version(1, 0, 0);
            var buildNumber = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
            var installDate = File.GetCreationTime(assembly.Location);

            return new VersionInfo(version, installDate, buildNumber);
        }

        private async Task<bool> InstallUpdateAsync(string updateFile, bool silent)
        {
            try
            {
                var updateBat = Path.Combine(_updateDirectory, "update.bat");
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                var updateScript = $@"
@echo off
timeout /t 2 /nobreak
powershell -Command ""Expand-Archive -Path '{updateFile}' -DestinationPath '{AppDomain.CurrentDomain.BaseDirectory}' -Force""
start """" ""{currentExe}""
del ""{updateFile}""
del ""%~f0""
";

                await File.WriteAllTextAsync(updateBat, updateScript);

                if (!silent)
                {
                    OnUpdateProgress("Installing", 50, "Waiting for user confirmation...");
                    // TODO: Show confirmation dialog
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = updateBat,
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                OnUpdateProgress("Installing", 100, "Update will be applied on next launch");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to install update");
                return false;
            }
        }

        private async Task CreateBackupAsync()
        {
            try
            {
                var currentDir = AppDomain.CurrentDomain.BaseDirectory;
                var backupFile = Path.Combine(_backupDirectory, $"backup_{DateTime.Now:yyyyMMddHHmmss}.zip");

                OnUpdateProgress("Creating backup", 0);
                await Task.Run(() => System.IO.Compression.ZipFile.CreateFromDirectory(currentDir, backupFile));
                OnUpdateProgress("Creating backup", 100);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create backup");
                throw;
            }
        }

        private void OnUpdateProgress(string operation, int progressPercentage, string? statusMessage = null)
        {
            UpdateProgress?.Invoke(this, new UpdateProgressEventArgs(operation, progressPercentage, statusMessage));
        }

        private void OnUpdateStatusChanged(UpdateStatus status, string? errorMessage = null)
        {
            UpdateStatusChanged?.Invoke(this, new UpdateStatusEventArgs(status, errorMessage));
        }

        private async Task<List<GitHubRelease>> GetReleasesAsync()
        {
            try
            {
                var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases";
                HttpRequestMessage? request = null;

                try
                {
                    request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("User-Agent", _userAgent);

                    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonSerializer.Deserialize<List<GitHubRelease>>(content) ?? new List<GitHubRelease>();
                }
                finally
                {
                    request?.Dispose();
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request error while getting releases");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while getting releases");
                throw;
            }
        }

        internal sealed class GitHubRelease
        {
            public string TagName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public bool Draft { get; set; }
            public bool Prerelease { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime PublishedAt { get; set; }
            public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
        }

        internal sealed class GitHubAsset
        {
            public string Name { get; set; } = string.Empty;
            public string BrowserDownloadUrl { get; set; } = string.Empty;
            public string ContentType { get; set; } = string.Empty;
            public long Size { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}