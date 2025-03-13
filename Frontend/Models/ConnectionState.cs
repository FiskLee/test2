using System;
using System.Collections.Generic;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class ConnectionState
    {
        public string ServerUrl { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public DateTime LastUpdate { get; set; }
        public NetworkStats? NetworkStats { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; } = new();
        public int ErrorCount { get; set; }
        public string? LastError { get; set; }
        public DateTime LastErrorTime { get; set; }
        public ConnectionStateType CurrentState { get; set; }
        public List<ConnectionAttempt> ConnectionHistory { get; set; } = new();
        public Dictionary<string, int> ErrorCounts { get; set; } = new();
        public DateTime ConnectionStartTime { get; set; }
        public TimeSpan ConnectionDuration { get; set; }
        public DateTime? LastSuccessfulConnectionTime { get; set; }
        public TimeSpan LastSuccessfulConnectionDuration { get; set; }
        public bool IsReconnecting { get; set; }
        public int ReconnectionAttempts { get; set; }

        public ConnectionState()
        {
            ServerUrl = string.Empty;
            Diagnostics = new Dictionary<string, object>();
            ConnectionHistory = new List<ConnectionAttempt>();
            ErrorCounts = new Dictionary<string, int>();
        }
    }

    public class ConnectionAttempt
    {
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; } = new();
    }

    public enum ConnectionStateType
    {
        Disconnected,
        Connecting,
        Connected,
        Error,
        Reconnecting
    }
}