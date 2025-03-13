using System;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for managing application updates and version control.
    /// Provides methods for checking, downloading, and installing updates.
    /// </summary>
    /// <remarks>
    /// Key responsibilities:
    /// - Checking for new versions
    /// - Managing update downloads
    /// - Handling update installation
    /// - Backup and rollback support
    /// 
    /// This service ensures the application stays up-to-date by managing
    /// the update lifecycle. It supports automatic updates and provides
    /// progress tracking for long-running update operations.
    /// 
    /// Usage example:
    /// ```csharp
    /// if (await _updateService.CheckForUpdatesAsync())
    /// {
    ///     await _updateService.DownloadAndInstallUpdateAsync();
    /// }
    /// ```
    /// </remarks>
    public interface IUpdateService
    {
        /// <summary>
        /// Checks if a new version is available.
        /// </summary>
        /// <returns>True if update available, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Fetches version info from update server
        /// 2. Compares with current version
        /// 3. Validates update compatibility
        /// 4. Returns update availability
        /// 
        /// The check respects the configured update channel (stable/beta).
        /// </remarks>
        Task<bool> CheckForUpdatesAsync();

        /// <summary>
        /// Gets information about the latest available version.
        /// </summary>
        /// <returns>Update information if available, null otherwise</returns>
        /// <remarks>
        /// Returns details about:
        /// - Version number
        /// - Release notes
        /// - Required system requirements
        /// - Download size
        /// - Release date
        /// </remarks>
        Task<Models.UpdateInfo?> GetLatestVersionInfoAsync();

        /// <summary>
        /// Downloads and installs the latest update.
        /// </summary>
        /// <param name="silent">Whether to install silently without user prompts</param>
        /// <returns>True if update successful, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Downloads the update package
        /// 2. Verifies package integrity
        /// 3. Creates backup of current version
        /// 4. Installs the update
        /// 5. Verifies installation
        /// 
        /// Progress is reported via UpdateProgress event.
        /// </remarks>
        Task<bool> DownloadAndInstallUpdateAsync(bool silent = false);

        /// <summary>
        /// Rolls back to the previous version.
        /// </summary>
        /// <returns>True if rollback successful, false otherwise</returns>
        /// <remarks>
        /// This method:
        /// 1. Validates backup availability
        /// 2. Restores previous version
        /// 3. Verifies restoration
        /// 
        /// Only available if a backup exists from previous update.
        /// </remarks>
        Task<bool> RollbackAsync();

        /// <summary>
        /// Gets the current application version.
        /// </summary>
        /// <returns>Current version information</returns>
        /// <remarks>
        /// Returns details about the currently installed version,
        /// including build number and installation date.
        /// </remarks>
        VersionInfo GetCurrentVersion();

        /// <summary>
        /// Event that fires when update progress changes.
        /// </summary>
        /// <remarks>
        /// Reports progress for:
        /// - Download progress
        /// - Verification steps
        /// - Installation progress
        /// - Rollback operations
        /// </remarks>
        event EventHandler<UpdateProgressEventArgs> UpdateProgress;

        /// <summary>
        /// Event that fires when update status changes.
        /// </summary>
        /// <remarks>
        /// Notifies subscribers of:
        /// - Update availability
        /// - Installation status
        /// - Error conditions
        /// - Rollback status
        /// </remarks>
        event EventHandler<UpdateStatusEventArgs> UpdateStatusChanged;
    }

    /// <summary>
    /// Contains information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// The version number of the update
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Release notes for the update
        /// </summary>
        public string ReleaseNotes { get; }

        /// <summary>
        /// Size of the update package in bytes
        /// </summary>
        public long DownloadSize { get; }

        /// <summary>
        /// When the update was released
        /// </summary>
        public DateTime ReleaseDate { get; }

        /// <summary>
        /// Whether the update is required
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Creates a new UpdateInfo instance.
        /// </summary>
        public UpdateInfo(Version version, string releaseNotes, long downloadSize, DateTime releaseDate, bool isRequired)
        {
            Version = version;
            ReleaseNotes = releaseNotes;
            DownloadSize = downloadSize;
            ReleaseDate = releaseDate;
            IsRequired = isRequired;
        }
    }

    /// <summary>
    /// Contains information about the current version.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// The current version number
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// When this version was installed
        /// </summary>
        public DateTime InstallDate { get; }

        /// <summary>
        /// The build number of this version
        /// </summary>
        public string BuildNumber { get; }

        /// <summary>
        /// Creates a new VersionInfo instance.
        /// </summary>
        public VersionInfo(Version version, DateTime installDate, string buildNumber)
        {
            Version = version;
            InstallDate = installDate;
            BuildNumber = buildNumber;
        }
    }

    /// <summary>
    /// Event arguments for update progress events.
    /// </summary>
    public class UpdateProgressEventArgs : EventArgs
    {
        /// <summary>
        /// The current operation being performed
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; }

        /// <summary>
        /// Optional status message
        /// </summary>
        public string? StatusMessage { get; }

        /// <summary>
        /// Creates a new UpdateProgressEventArgs instance.
        /// </summary>
        public UpdateProgressEventArgs(string operation, int progressPercentage, string? statusMessage = null)
        {
            Operation = operation;
            ProgressPercentage = progressPercentage;
            StatusMessage = statusMessage;
        }
    }

    /// <summary>
    /// Event arguments for update status changes.
    /// </summary>
    public class UpdateStatusEventArgs : EventArgs
    {
        /// <summary>
        /// The current update status
        /// </summary>
        public UpdateStatus Status { get; }

        /// <summary>
        /// Optional error message
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Creates a new UpdateStatusEventArgs instance.
        /// </summary>
        public UpdateStatusEventArgs(UpdateStatus status, string? errorMessage = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Enumeration of possible update statuses.
    /// </summary>
    public enum UpdateStatus
    {
        /// <summary>
        /// No update is available
        /// </summary>
        NoUpdateAvailable,

        /// <summary>
        /// Update is available for download
        /// </summary>
        UpdateAvailable,

        /// <summary>
        /// Update is being downloaded
        /// </summary>
        Downloading,

        /// <summary>
        /// Update is being installed
        /// </summary>
        Installing,

        /// <summary>
        /// Update completed successfully
        /// </summary>
        Complete,

        /// <summary>
        /// Update failed
        /// </summary>
        Failed,

        /// <summary>
        /// Update is being rolled back
        /// </summary>
        RollingBack
    }
}