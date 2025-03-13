using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class NetworkStats
    {
        public bool IsNetworkAvailable { get; set; }
        public bool IsDnsAvailable { get; set; }
        public bool IsPortAccessible { get; set; }
        public double Latency { get; set; }
        public string NetworkType { get; set; } = string.Empty;
        public string NetworkSpeed { get; set; } = string.Empty;
        public double Jitter { get; set; }
        public double Bandwidth { get; set; }
        public string Quality { get; set; } = string.Empty;
        public bool PingSuccess { get; set; }
        public double PingTime { get; set; }
        public double PacketLoss { get; set; }
        private readonly List<string> _dnsServers = new List<string>();
        public ReadOnlyCollection<string> DnsServers => _dnsServers.AsReadOnly();
        public string DnsSuffix { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public int ConnectionCount { get; set; }
        public DateTime LastUpdate { get; set; }
        public double BytesReceived { get; set; }
        public double BytesSent { get; set; }
        public double DownloadSpeed { get; set; }
        public double UploadSpeed { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; }
        public int PacketsReceived { get; set; }
        public int PacketsSent { get; set; }

        public NetworkStats()
        {
            Diagnostics = new Dictionary<string, object>();
            LastUpdate = DateTime.UtcNow;
        }
    }
}