using Serilog;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DiskMetricsViewModel
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DiskMetricsViewModel>();

        private string _diskName = string.Empty;
        private float _diskReadMBps;
        private float _diskWriteMBps;
        private float _diskUsagePercentage;

        // Ensure DiskName is never null by providing a default value.
        public string DiskName
        {
            get => _diskName;
            set
            {
                _logger.Verbose("Updating DiskName from '{OldName}' to '{NewName}'", _diskName, value);
                _diskName = value;
                LogMetrics();
            }
        }

        // Add any additional properties required for disk metrics.
        public float DiskReadMBps
        {
            get => _diskReadMBps;
            set
            {
                _logger.Verbose("Updating DiskReadMBps from {OldValue} to {NewValue} MB/s", _diskReadMBps, value);
                _diskReadMBps = value;
                LogMetrics();
            }
        }

        public float DiskWriteMBps
        {
            get => _diskWriteMBps;
            set
            {
                _logger.Verbose("Updating DiskWriteMBps from {OldValue} to {NewValue} MB/s", _diskWriteMBps, value);
                _diskWriteMBps = value;
                LogMetrics();
            }
        }

        public float DiskUsagePercentage
        {
            get => _diskUsagePercentage;
            set
            {
                _logger.Verbose("Updating DiskUsagePercentage from {OldValue} to {NewValue}%", _diskUsagePercentage, value);
                _diskUsagePercentage = value;
                LogMetrics();
            }
        }

        public DiskMetricsViewModel()
        {
            _logger.Verbose("Initializing DiskMetricsViewModel with default values");
            LogMetrics();
        }

        private void LogMetrics()
        {
            _logger.Verbose("Current Disk Metrics - Name: {Name}, Read: {Read} MB/s, Write: {Write} MB/s, Usage: {Usage}%",
                DiskName,
                DiskReadMBps,
                DiskWriteMBps,
                DiskUsagePercentage);
        }
    }
}
