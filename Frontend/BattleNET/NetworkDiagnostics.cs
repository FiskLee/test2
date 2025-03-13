using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace BattleNET
{
    public class BattlEyeNetworkDiagnostics : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, NetworkMeasurement> _measurements;
        private readonly SemaphoreSlim _measurementLock;
        private bool _isDisposed;
        private const int MAX_HISTORY_SIZE = 100;

        public BattlEyeNetworkDiagnostics(Serilog.ILogger logger)
        {
            _logger = logger ?? Log.ForContext<BattlEyeNetworkDiagnostics>();
            _measurements = new ConcurrentDictionary<string, NetworkMeasurement>();
            _measurementLock = new SemaphoreSlim(1, 1);
        }

        public class NetworkMetric
        {
            public string Name { get; }
            public double Value { get; private set; }
            public string Unit { get; }
            public DateTime Timestamp { get; private set; }
            public List<double> History { get; }

            public NetworkMetric(string name, double value, string unit)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                Value = value;
                Unit = unit ?? throw new ArgumentNullException(nameof(unit));
                Timestamp = DateTime.UtcNow;
                History = new List<double>();
            }

            public void Update(double newValue)
            {
                History.Add(Value);
                if (History.Count > MAX_HISTORY_SIZE)
                {
                    History.RemoveAt(0);
                }
                Value = newValue;
                Timestamp = DateTime.UtcNow;
            }
        }

        public class NetworkHealthReport
        {
            public DateTime Timestamp { get; set; }
            public Dictionary<string, NetworkMetric> Metrics { get; set; }
            public List<string> Issues { get; set; }
            public List<string> Recommendations { get; set; }
            public string OverallHealth { get; set; }

            public NetworkHealthReport()
            {
                Timestamp = DateTime.UtcNow;
                Metrics = new Dictionary<string, NetworkMetric>();
                Issues = new List<string>();
                Recommendations = new List<string>();
                OverallHealth = string.Empty;
            }
        }

        public async Task<NetworkHealthReport> GenerateReportAsync()
        {
            await _measurementLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var report = new NetworkHealthReport
                {
                    Metrics = _measurements.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new NetworkMetric(kvp.Key, kvp.Value.Value, kvp.Value.Unit))
                };

                // Collect metrics
                await CollectMetricsAsync().ConfigureAwait(false);

                // Analyze issues
                AnalyzeIssues(report);

                // Generate recommendations
                GenerateRecommendations(report);

                // Determine overall health
                report.OverallHealth = DetermineOverallHealth(report);

                return report;
            }
            finally
            {
                _measurementLock.Release();
            }
        }

        private async Task CollectMetricsAsync()
        {
            try
            {
                // Measure latency
                await MeasureLatencyAsync().ConfigureAwait(false);

                // Measure bandwidth
                await MeasureBandwidthAsync().ConfigureAwait(false);

                // Measure packet loss
                await MeasurePacketLossAsync().ConfigureAwait(false);

                // Check network interfaces
                CheckNetworkInterfaces();

                // Check DNS resolution
                await CheckDnsResolutionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error collecting network metrics");
                throw;
            }
        }

        private async Task MeasureLatencyAsync()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1000).ConfigureAwait(false);

                UpdateMetric("Latency", reply.RoundtripTime, "ms");
                UpdateMetric("PingSuccess", reply.Status == IPStatus.Success ? 1 : 0, "boolean");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error measuring latency");
                throw;
            }
        }

        private async Task MeasureBandwidthAsync()
        {
            try
            {
                // Implement bandwidth measurement logic
                // This is a simplified version
                await Task.CompletedTask;
                UpdateMetric("Bandwidth", 1000, "Mbps");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error measuring bandwidth");
                throw;
            }
        }

        private async Task MeasurePacketLossAsync()
        {
            try
            {
                using var ping = new Ping();
                var lostPackets = 0;
                var totalPackets = 10;

                for (int i = 0; i < totalPackets; i++)
                {
                    var reply = await ping.SendPingAsync("8.8.8.8", 1000).ConfigureAwait(false);
                    if (reply.Status != IPStatus.Success)
                    {
                        lostPackets++;
                    }
                    await Task.Delay(100).ConfigureAwait(false);
                }

                var packetLoss = (double)lostPackets / totalPackets * 100;
                UpdateMetric("PacketLoss", packetLoss, "%");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error measuring packet loss");
                throw;
            }
        }

        private void CheckNetworkInterfaces()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var activeInterfaces = interfaces.Count(nic => nic.OperationalStatus == OperationalStatus.Up);

                UpdateMetric("ActiveInterfaces", activeInterfaces, "count");
                UpdateMetric("TotalInterfaces", interfaces.Length, "count");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking network interfaces");
                throw;
            }
        }

        private async Task CheckDnsResolutionAsync()
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync("google.com").ConfigureAwait(false);
                UpdateMetric("DnsResolution", hostEntry.AddressList.Length > 0 ? 1 : 0, "boolean");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking DNS resolution");
                UpdateMetric("DnsResolution", 0, "boolean");
                throw;
            }
        }

        private void UpdateMetric(string name, double value, string unit)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Metric name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrEmpty(unit))
            {
                throw new ArgumentException("Unit cannot be null or empty", nameof(unit));
            }

            _measurements.AddOrUpdate(
                name,
                new NetworkMeasurement(name, value, unit),
                (_, measurement) =>
                {
                    measurement.AddToHistory(value);
                    return measurement;
                });
        }

        private void AnalyzeIssues(NetworkHealthReport report)
        {
            if (_measurements.TryGetValue("Latency", out var latency) && latency.Value > 200)
            {
                report.Issues.Add("High latency detected");
            }

            if (_measurements.TryGetValue("PacketLoss", out var packetLoss) && packetLoss.Value > 5)
            {
                report.Issues.Add("High packet loss detected");
            }

            if (_measurements.TryGetValue("DnsResolution", out var dns) && dns.Value == 0)
            {
                report.Issues.Add("DNS resolution issues detected");
            }
        }

        private void GenerateRecommendations(NetworkHealthReport report)
        {
            foreach (var issue in report.Issues)
            {
                switch (issue)
                {
                    case "High latency detected":
                        report.Recommendations.Add("Consider using a different network connection or closer server");
                        break;
                    case "High packet loss detected":
                        report.Recommendations.Add("Check network stability and consider using a wired connection");
                        break;
                    case "DNS resolution issues detected":
                        report.Recommendations.Add("Try using alternative DNS servers or check DNS configuration");
                        break;
                }
            }
        }

        private string DetermineOverallHealth(NetworkHealthReport report)
        {
            if (report.Issues.Count == 0)
            {
                return "Excellent";
            }
            else if (report.Issues.Count <= 2)
            {
                return "Good";
            }
            else if (report.Issues.Count <= 4)
            {
                return "Fair";
            }
            else
            {
                return "Poor";
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _measurementLock.Dispose();
                _measurements.Clear();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during disposal");
                throw;
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }

}