using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DnsResolutionInfo
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> IPAddresses { get; set; } = new();
    }

    public class NetworkQualityMetrics
    {
        public double Jitter { get; set; }
        public double Bandwidth { get; set; }
        public string Quality { get; set; } = string.Empty;
    }

    public class NetworkInterfaceDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> IPAddresses { get; set; } = new();
        public List<string> IPv6Addresses { get; set; } = new();
        public string MACAddress { get; set; } = string.Empty;
        public bool IsOperational { get; set; }
        public bool SupportsMulticast { get; set; }
    }

    public static class NetworkQualityHelper
    {
        public static double CalculateJitter(double pingTime)
        {
            // Simple jitter calculation based on ping time
            return pingTime * 0.1; // 10% of ping time as jitter
        }

        public static double GetNetworkBandwidth()
        {
            // Get network bandwidth in Mbps
            double bandwidth = 0;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    var stats = nic.GetIPStatistics();
                    bandwidth += stats.BytesReceived + stats.BytesSent;
                }
            }
            return bandwidth / (1024 * 1024); // Convert to Mbps
        }

        public static string AssessNetworkQuality(double pingTime, bool pingSuccess)
        {
            if (!pingSuccess)
                return "Poor";

            if (pingTime < 50)
                return "Excellent";
            if (pingTime < 100)
                return "Good";
            if (pingTime < 200)
                return "Fair";
            return "Poor";
        }
    }

    public class NetworkStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    internal class NetworkException : Exception
    {
        public NetworkException() { }
        public NetworkException(string message, Exception innerException) : base(message, innerException) { }
    }
}