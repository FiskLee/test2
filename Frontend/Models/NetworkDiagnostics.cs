using System;
using System.Collections.Generic;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    internal class NetworkDiagnostics
    {
        public bool IsNetworkAvailable { get; set; }
        public bool IsDnsAvailable { get; set; }
        public bool IsPortAccessible { get; set; }
        public string NetworkStatus { get; set; } = string.Empty;
        public string DnsStatus { get; set; } = string.Empty;
        public string PortStatus { get; set; } = string.Empty;
        public List<NetworkInterfaceDetails> NetworkInterfaces { get; set; }
        public List<NetworkInterfaceDetails> AvailableInterfaces { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Latency { get; set; }
        public double PingTime { get; set; }
        public bool PingSuccess { get; set; }
        public required string PingError { get; set; }
        public bool PortOpen { get; set; }
        public required string PortError { get; set; }
        public required string NetworkType { get; set; }
        public required string NetworkSpeed { get; set; }
        public List<string> ResolvedAddresses { get; set; }
        public required string DnsError { get; set; }
        public required string InterfaceError { get; set; }
        public Dictionary<string, object> Diagnostics { get; set; }
        public required string NetworkQuality { get; set; }
        public DnsResolutionResult DnsResolution { get; set; }
        public bool CanResolveDns => string.IsNullOrEmpty(DnsError);
        public bool CanAccessPort => string.IsNullOrEmpty(PortError);

        public NetworkDiagnostics()
        {
            NetworkInterfaces = new List<NetworkInterfaceDetails>();
            AvailableInterfaces = new List<NetworkInterfaceDetails>();
            Timestamp = DateTime.UtcNow;
            ResolvedAddresses = new List<string>();
            Diagnostics = new Dictionary<string, object>();
            DnsResolution = new DnsResolutionResult
            {
                Success = true,
                DnsError = string.Empty,
                InterfaceError = string.Empty
            };
            PingError = string.Empty;
            PortError = string.Empty;
            NetworkType = string.Empty;
            NetworkSpeed = string.Empty;
            DnsError = string.Empty;
            InterfaceError = string.Empty;
            NetworkQuality = string.Empty;
        }
    }

    public class DnsResolutionResult
    {
        public bool Success { get; set; }
        public required string DnsError { get; set; }
        public required string InterfaceError { get; set; }
    }
}