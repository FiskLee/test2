using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public enum UpdateStatus
    {
        Idle,
        Checking,
        Downloading,
        Installing,
        Completed,
        Failed,
        UpdateAvailable
    }

    public class UpdateStatusEventArgs : EventArgs
    {
        public UpdateStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class UpdateProgressEventArgs : EventArgs
    {
        public double Progress { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}