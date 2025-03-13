using Serilog;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Contains information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<UpdateInfo>();

        /// <summary>
        /// The version of the update.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Release notes for the update.
        /// </summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>
        /// Size of the update download in bytes.
        /// </summary>
        public long DownloadSize { get; set; }

        /// <summary>
        /// When the update was published.
        /// </summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Whether this is a pre-release version.
        /// </summary>
        public bool IsPrerelease { get; set; }

        /// <summary>
        /// URL to download the update.
        /// </summary>
        public Uri DownloadUrl { get; set; } = new Uri("http://example.com");

        /// <summary>
        /// Initializes a new instance of the UpdateInfo class.
        /// </summary>
        public UpdateInfo()
        {
            _logger.Verbose("Initializing UpdateInfo with default values");
            LogUpdateInfo();
        }

        /// <summary>
        /// Initializes a new instance of the UpdateInfo class with the specified parameters.
        /// </summary>
        public UpdateInfo(string version, string releaseNotes, long downloadSize, DateTime publishedAt, bool isPrerelease, Uri downloadUrl)
        {
            _logger.Verbose("Initializing UpdateInfo with parameters - Version: {Version}, Size: {Size}MB, Published: {Published}, Pre-release: {PreRelease}",
                version,
                downloadSize / (1024.0 * 1024.0),
                publishedAt,
                isPrerelease);

            Version = version;
            ReleaseNotes = releaseNotes;
            DownloadSize = downloadSize;
            PublishedAt = publishedAt;
            IsPrerelease = isPrerelease;
            DownloadUrl = downloadUrl;

            LogUpdateInfo();
        }

        private void LogUpdateInfo()
        {
            _logger.Verbose("Update Info - Version: {Version}, Size: {Size}MB, Published: {Published}, Pre-release: {PreRelease}",
                Version,
                DownloadSize / (1024.0 * 1024.0),
                PublishedAt,
                IsPrerelease);

            _logger.Verbose("Release Notes Length: {Length}, Download URL: {Url}",
                ReleaseNotes.Length,
                DownloadUrl);

            if (!string.IsNullOrEmpty(ReleaseNotes))
            {
                _logger.Verbose("Release Notes Preview: {Preview}",
                    ReleaseNotes.Length > 100 ? ReleaseNotes.Substring(0, 100) + "..." : ReleaseNotes);
            }
        }
    }
}