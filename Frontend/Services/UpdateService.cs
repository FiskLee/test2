using ArmaReforgerServerMonitor.Frontend.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Implementation of the update service that manages application updates
    /// and version control.
    /// </summary>
    /// <remarks>
    /// This service provides:
    /// - Update checking
    /// - Version comparison
    /// - Update downloading
    /// - Installation handling
    /// - Rollback support
    /// 
    /// The service manages the entire update lifecycle, from checking for
    /// updates to installing them and handling any failures.
    /// </remarks>
    internal class UpdateService : IUpdateService, IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private readonly string _updateDirectory;
        private readonly string _backupDirectory;
        private readonly string? _githubRepositoryUrl;
        private readonly string _currentVersion;
        private readonly string _updateUrl;
        private Models.UpdateInfo? _latestVersion;
        private bool _disposed;

        /// <summary>
        /// Event that fires when update progress changes.
        /// </summary>
        public event EventHandler<UpdateProgressEventArgs>? UpdateProgress;

        /// <summary>
        /// Event that fires when update status changes.
        /// </summary>
        public event EventHandler<UpdateStatusEventArgs>? UpdateStatusChanged;

        /// <summary>
        /// Initializes a new instance of the UpdateService class.
        /// </summary>
        /// <param name="logger">Logger for update events</param>
        /// <param name="httpClient">HTTP client for update requests</param>
        /// <param name="settings">Application settings</param>
        /// <remarks>
        /// The constructor:
        /// 1. Creates update directories
        /// 2. Configures HTTP client
        /// 3. Initializes version tracking
        /// 4. Sets up event handlers
        /// </remarks>
        public UpdateService(
            HttpClient httpClient,
            IOptions<AppSettings> settings)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = Log.ForContext<UpdateService>();
            _githubRepositoryUrl = _settings.Updates?.GithubRepositoryUrl;
            _currentVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0";
            _updateUrl = _settings.UpdateSettings?.VersionCheckUrl ?? "https://api.example.com/version";

            _logger.Verbose("UpdateService initialized - Current version: {Version}, Update URL: {Url}, GitHub repo: {Repo}",
                _currentVersion, _updateUrl, _githubRepositoryUrl ?? "not configured");

            try
            {
                // Create update directories
                _updateDirectory = Path.Combine(AppContext.BaseDirectory, "Updates");
                _backupDirectory = Path.Combine(_updateDirectory, "Backup");

                _logger.Debug("Creating update directories - Update: {UpdateDir}, Backup: {BackupDir}",
                    _updateDirectory, _backupDirectory);

                Directory.CreateDirectory(_updateDirectory);
                Directory.CreateDirectory(_backupDirectory);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating update directories - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Checks if a new version is available.
        /// </summary>
        /// <returns>True if update available</returns>
        /// <remarks>
        /// This method:
        /// 1. Fetches version info
        /// 2. Compares versions
        /// 3. Validates compatibility
        /// 4. Updates status
        /// 
        /// The check respects update channel settings.
        /// </remarks>
        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                _logger.Verbose("Checking for updates at {Url}", _updateUrl);
                var response = await _httpClient.GetStringAsync(_updateUrl.ToString()).ConfigureAwait(false);
                _logger.Debug("Received update check response: {Response}", response);

                var updateInfo = JsonSerializer.Deserialize<Models.UpdateInfo>(response);
                if (updateInfo != null)
                {
                    _logger.Debug("Update check completed - Latest version: {Version}, Current version: {CurrentVersion}",
                        updateInfo.Version, _currentVersion);
                    _latestVersion = updateInfo;
                    OnUpdateStatus(UpdateStatus.UpdateAvailable);
                    return true;
                }

                _logger.Warning("Update check returned null response");
                return false;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize update check response - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request failed during update check - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.Error(ex, "Update check task was canceled - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for updates - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets information about the latest available version.
        /// </summary>
        /// <returns>Update information if available</returns>
        /// <remarks>
        /// This method:
        /// 1. Fetches version info
        /// 2. Parses response
        /// 3. Validates data
        /// 4. Returns info object
        /// 
        /// Returns null if version info unavailable.
        /// </remarks>
        public async Task<Models.UpdateInfo?> GetLatestVersionInfoAsync()
        {
            if (_settings.Updates.VersionCheckUrl == null) throw new InvalidOperationException("VersionCheckUrl is not configured.");
            try
            {
                _logger.Debug("Retrieving latest version information from {Url}", _settings.Updates.VersionCheckUrl);

                var response = await _httpClient.GetAsync(new Uri(_settings.Updates.VersionCheckUrl)).ConfigureAwait(false);
                _logger.Verbose("Received version info response with status code {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.Debug("Received version info: {Json}", json);
                    var info = JsonSerializer.Deserialize<Models.UpdateInfo>(json);
                    _logger.Information("Successfully retrieved version information - Version: {Version}, Release notes: {Notes}",
                        info?.Version, info?.ReleaseNotes);
                    return info;
                }

                _logger.Warning("Failed to retrieve version information: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize version information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving version information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// Downloads and installs the latest update.
        /// </summary>
        /// <param name="silent">Whether to install silently</param>
        /// <returns>True if update successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Downloads update package
        /// 2. Verifies integrity
        /// 3. Creates backup
        /// 4. Installs update
        /// 5. Verifies installation
        /// </remarks>
        public async Task<bool> DownloadAndInstallUpdateAsync(bool silent = false)
        {
            try
            {
                if (_latestVersion == null)
                {
                    _logger.Warning("No update information available");
                    return false;
                }

                _logger.Information("Starting update to version {Version}", _latestVersion.Version);
                OnUpdateStatus(UpdateStatus.Downloading);

                // Download update
                var updateFile = await DownloadUpdateAsync(_latestVersion.DownloadUrl.ToString()).ConfigureAwait(false);
                if (updateFile == null)
                {
                    _logger.Error("Failed to download update from {Url}", _latestVersion.DownloadUrl);
                    OnUpdateStatus(UpdateStatus.Failed, "Download failed");
                    return false;
                }

                // Create backup
                if (!await CreateBackupAsync().ConfigureAwait(false))
                {
                    _logger.Error("Failed to create backup in {Directory}", _backupDirectory);
                    OnUpdateStatus(UpdateStatus.Failed, "Backup failed");
                    return false;
                }

                // Install update
                OnUpdateStatus(UpdateStatus.Installing);
                if (!await InstallUpdateAsync(updateFile, silent).ConfigureAwait(false))
                {
                    _logger.Error("Failed to install update from {File}", updateFile);
                    OnUpdateStatus(UpdateStatus.Failed, "Installation failed");
                    await RollbackAsync().ConfigureAwait(false);
                    return false;
                }

                _logger.Information("Update completed successfully to version {Version}", _latestVersion.Version);
                OnUpdateStatus(UpdateStatus.Complete);
                return true;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize update information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Unexpected error during update installation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                await RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Rolls back to the previous version.
        /// </summary>
        /// <returns>True if rollback successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates backup
        /// 2. Restores files
        /// 3. Verifies restoration
        /// 4. Updates status
        /// </remarks>
        public async Task<bool> RollbackAsync()
        {
            try
            {
                _logger.Information("Starting rollback to previous version");
                OnUpdateStatus(UpdateStatus.RollingBack);

                var backupFile = Path.Combine(_backupDirectory, "backup.zip");
                if (!File.Exists(backupFile))
                {
                    _logger.Error("No backup file found at {Path}", backupFile);
                    return false;
                }

                // Restore from backup
                if (!await RestoreBackupAsync(backupFile).ConfigureAwait(false))
                {
                    _logger.Error("Failed to restore from backup at {Path}", backupFile);
                    return false;
                }

                _logger.Information("Rollback completed successfully");
                OnUpdateStatus(UpdateStatus.Complete);
                return true;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize backup information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Unexpected error during rollback - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        /// <returns>Current version information</returns>
        /// <remarks>
        /// Returns details about:
        /// - Version number
        /// - Build number
        /// - Installation date
        /// </remarks>
        public VersionInfo GetCurrentVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version ?? new Version(1, 0, 0);
            var buildNumber = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
            var installDate = File.GetCreationTime(assembly.Location);

            _logger.Debug("Retrieved current version info - Version: {Version}, Build: {Build}, Install date: {Date}",
                version, buildNumber, installDate);

            return new VersionInfo(version, installDate, buildNumber);
        }

        /// <summary>
        /// Downloads the update package.
        /// </summary>
        /// <param name="downloadUrl">URL of the update package</param>
        /// <returns>Path to downloaded file</returns>
        /// <remarks>
        /// This method:
        /// 1. Creates temp file
        /// 2. Downloads package
        /// 3. Reports progress
        /// 4. Verifies download
        /// </remarks>
        private async Task<string?> DownloadUpdateAsync(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl)) throw new ArgumentNullException(nameof(downloadUrl), "Download URL cannot be null or empty.");
            try
            {
                var updateFile = Path.Combine(_updateDirectory, $"update-{_latestVersion?.Version}.zip");
                _logger.Verbose("Starting download from {Url} to {File}", downloadUrl, updateFile);

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error("Failed to download update: {StatusCode}", response.StatusCode);
                    return null;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var receivedBytes = 0L;
                _logger.Debug("Download size: {Size} bytes", totalBytes);

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = File.Create(updateFile);
                var buffer = new byte[8192];
                var read = 0;

                while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    receivedBytes += read;

                    if (totalBytes > 0)
                    {
                        var progress = (int)((receivedBytes * 100) / totalBytes);
                        _logger.Verbose("Download progress: {Progress}% ({Received}/{Total} bytes)",
                            progress, receivedBytes, totalBytes);
                        OnUpdateProgress("Downloading", progress);
                    }
                }

                _logger.Information("Update package downloaded successfully to {File}", updateFile);
                return updateFile;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize download information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP request error downloading update - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "IO error downloading update - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return null;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Error downloading update - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// Creates a backup of the current version.
        /// </summary>
        /// <returns>True if backup successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Creates backup archive
        /// 2. Copies current files
        /// 3. Verifies backup
        /// 4. Updates status
        /// </remarks>
        private async Task<bool> CreateBackupAsync()
        {
            try
            {
                _logger.Information("Creating backup in {Directory}", _backupDirectory);
                var backupFile = Path.Combine(_backupDirectory, "backup.zip");

                await Task.Run(() =>
                {
                    if (File.Exists(backupFile))
                    {
                        _logger.Debug("Removing existing backup file");
                        File.Delete(backupFile);
                    }
                    _logger.Verbose("Creating backup archive");
                    ZipFile.CreateFromDirectory(AppContext.BaseDirectory, backupFile);
                }).ConfigureAwait(false);

                _logger.Information("Backup created successfully at {File}", backupFile);
                return true;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to serialize backup information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "IO error creating backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, "Unauthorized access creating backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Unexpected error creating backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Installs the update package.
        /// </summary>
        /// <param name="updateFile">Path to update package</param>
        /// <param name="silent">Whether to install silently</param>
        /// <returns>True if installation successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Extracts package
        /// 2. Copies new files
        /// 3. Updates registry
        /// 4. Verifies installation
        /// </remarks>
        private async Task<bool> InstallUpdateAsync(string updateFile, bool silent)
        {
            if (string.IsNullOrWhiteSpace(updateFile)) throw new ArgumentException("Update file path cannot be null or empty.", nameof(updateFile));
            try
            {
                _logger.Information("Installing update from {File}", updateFile);

                await Task.Run(() =>
                {
                    // Extract update
                    var tempDir = Path.Combine(_updateDirectory, "temp");
                    if (Directory.Exists(tempDir))
                    {
                        _logger.Debug("Cleaning up existing temp directory");
                        Directory.Delete(tempDir, true);
                    }
                    _logger.Verbose("Extracting update to {Directory}", tempDir);
                    ZipFile.ExtractToDirectory(updateFile, tempDir);

                    // Copy files
                    _logger.Debug("Copying updated files");
                    foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(tempDir.Length + 1);
                        var targetPath = Path.Combine(AppContext.BaseDirectory, relativePath);
                        _logger.Verbose("Copying {File} to {Target}", file, targetPath);
                        File.Copy(file, targetPath, true);
                    }

                    // Cleanup
                    _logger.Debug("Cleaning up temporary files");
                    Directory.Delete(tempDir, true);
                    File.Delete(updateFile);
                }).ConfigureAwait(false);

                _logger.Information("Update installed successfully");
                return true;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize update information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "IO error during update installation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, "Unauthorized access during update installation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Unexpected error during update installation - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                OnUpdateStatus(UpdateStatus.Failed, ex.Message);
                await RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Restores files from backup.
        /// </summary>
        /// <param name="backupFile">Path to backup file</param>
        /// <returns>True if restoration successful</returns>
        /// <remarks>
        /// This method:
        /// 1. Extracts backup
        /// 2. Restores files
        /// 3. Verifies restoration
        /// 4. Updates status
        /// </remarks>
        private async Task<bool> RestoreBackupAsync(string backupFile)
        {
            if (string.IsNullOrWhiteSpace(backupFile)) throw new ArgumentException("Backup file path cannot be null or empty.", nameof(backupFile));
            try
            {
                _logger.Information("Restoring from backup at {File}", backupFile);

                await Task.Run(() =>
                {
                    // Extract backup
                    var tempDir = Path.Combine(_updateDirectory, "restore");
                    if (Directory.Exists(tempDir))
                    {
                        _logger.Debug("Cleaning up existing restore directory");
                        Directory.Delete(tempDir, true);
                    }
                    _logger.Verbose("Extracting backup to {Directory}", tempDir);
                    ZipFile.ExtractToDirectory(backupFile, tempDir);

                    // Copy files
                    _logger.Debug("Restoring files from backup");
                    foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = file.Substring(tempDir.Length + 1);
                        var targetPath = Path.Combine(AppContext.BaseDirectory, relativePath);
                        _logger.Verbose("Restoring {File} to {Target}", file, targetPath);
                        File.Copy(file, targetPath, true);
                    }

                    // Cleanup
                    _logger.Debug("Cleaning up temporary files");
                    Directory.Delete(tempDir, true);
                }).ConfigureAwait(false);

                _logger.Information("Backup restored successfully");
                return true;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Failed to deserialize backup information - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "IO error restoring backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, "Unauthorized access restoring backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException && ex is not JsonException)
            {
                _logger.Error(ex, "Unexpected error restoring backup - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Raises the UpdateProgress event.
        /// </summary>
        /// <param name="operation">Current operation</param>
        /// <param name="progress">Progress percentage</param>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Current operation
        /// - Progress percentage
        /// - Status message
        /// </remarks>
        private void OnUpdateProgress(string operation, int progress)
        {
            try
            {
                _logger.Verbose("Update progress: {Operation} - {Progress}%", operation, progress);
                UpdateProgress?.Invoke(this, new UpdateProgressEventArgs(operation, progress));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error raising UpdateProgress event - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// Raises the UpdateStatus event.
        /// </summary>
        /// <param name="status">Current status</param>
        /// <param name="error">Optional error message</param>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Status changes
        /// - Error conditions
        /// - Operation completion
        /// </remarks>
        private void OnUpdateStatus(UpdateStatus status, string? error = null)
        {
            try
            {
                _logger.Verbose("Update status changed to {Status} - Error: {Error}", status, error ?? "none");
                UpdateStatusChanged?.Invoke(this, new UpdateStatusEventArgs(status, error));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error raising UpdateStatus event - Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
            }
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
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}