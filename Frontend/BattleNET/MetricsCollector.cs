using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace BattleNET
{
    public class MetricsCollector : IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _metricsLock;
        private readonly ILogger _logger;
        private readonly Dictionary<string, List<MetricPoint>> _metrics;
        private readonly TimeSpan _collectionInterval;
        private readonly int _maxDataPoints;
        private const int MAX_HISTORY_SIZE = 100;

        public MetricsCollector(ILogger logger, TimeSpan collectionInterval, int maxDataPoints = 1000)
        {
            _logger = logger;
            _collectionInterval = collectionInterval;
            _maxDataPoints = maxDataPoints;
            _metrics = new Dictionary<string, List<MetricPoint>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _metricsLock = new SemaphoreSlim(1, 1);
        }

        public async Task StartAsync()
        {
            try
            {
                await Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await CollectMetricsAsync().ConfigureAwait(false);
                        await Task.Delay(_collectionInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }, _cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Metrics collection stopped");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in metrics collection");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        private async Task CollectMetricsAsync()
        {
            try
            {
                await _metricsLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var timestamp = DateTime.UtcNow;
                    var metrics = await GatherMetricsAsync().ConfigureAwait(false);

                    foreach (var metric in metrics)
                    {
                        if (!_metrics.ContainsKey(metric.Key))
                        {
                            _metrics[metric.Key] = new List<MetricPoint>();
                        }

                        _metrics[metric.Key].Add(new MetricPoint(metric.Value, timestamp));

                        // Trim old data points
                        if (_metrics[metric.Key].Count > _maxDataPoints)
                        {
                            _metrics[metric.Key].RemoveRange(0, _metrics[metric.Key].Count - _maxDataPoints);
                        }
                    }
                }
                finally
                {
                    _metricsLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error collecting metrics");
                throw;
            }
        }

        public Task<Dictionary<string, double>> GatherMetricsAsync()
        {
            var metrics = new Dictionary<string, double>();

            // CPU Usage
            metrics["cpu"] = GetCpuUsageAsync().Result;

            // Memory Usage
            metrics["memory"] = GetMemoryUsageAsync().Result;

            // Network I/O
            var networkStats = GetNetworkStatsAsync().Result;
            metrics["network_in"] = networkStats.BytesReceived;
            metrics["network_out"] = networkStats.BytesSent;

            // Disk I/O
            var diskStats = GetDiskStatsAsync().Result;
            metrics["disk_read"] = diskStats.BytesRead;
            metrics["disk_write"] = diskStats.BytesWritten;

            return Task.FromResult(metrics);
        }

        private Task<double> GetCpuUsageAsync()
        {
            // Implement CPU usage measurement
            return Task.FromResult(0.0);
        }

        private Task<double> GetMemoryUsageAsync()
        {
            // Implement memory usage measurement
            return Task.FromResult(0.0);
        }

        private Task<NetworkStats> GetNetworkStatsAsync()
        {
            // Implement network stats measurement
            return Task.FromResult(new NetworkStats());
        }

        private Task<DiskStats> GetDiskStatsAsync()
        {
            // Implement disk stats measurement
            return Task.FromResult(new DiskStats());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
                _metricsLock?.Dispose();
            }
        }

        public class MetricData
        {
            public string Name { get; }
            public double Value { get; private set; }
            public string Unit { get; }
            public DateTime Timestamp { get; private set; }
            public List<MetricPoint> History { get; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public double AverageValue { get; private set; }

            public MetricData(string name, double value, string unit)
            {
                Name = name;
                Value = value;
                Unit = unit;
                Timestamp = DateTime.UtcNow;
                History = new List<MetricPoint>();
                MinValue = value;
                MaxValue = value;
                AverageValue = value;
            }

            public void Update(double newValue)
            {
                History.Add(new MetricPoint(Value, Timestamp));
                if (History.Count > MAX_HISTORY_SIZE)
                {
                    History.RemoveAt(0);
                }

                Value = newValue;
                Timestamp = DateTime.UtcNow;

                // Update statistics
                MinValue = Math.Min(MinValue, newValue);
                MaxValue = Math.Max(MaxValue, newValue);
                AverageValue = History.Count > 0 ? History.Average(x => x.Value) : newValue;
            }
        }

        public class MetricPoint
        {
            public double Value { get; }
            public DateTime Timestamp { get; }

            public MetricPoint(double value, DateTime timestamp)
            {
                Value = value;
                Timestamp = timestamp;
            }
        }

        public class MetricsReport
        {
            public DateTime Timestamp { get; set; }
            public Dictionary<string, MetricData> Metrics { get; set; } = new Dictionary<string, MetricData>();
            public List<string> Warnings { get; set; } = new List<string>();
            public string OverallStatus { get; set; } = string.Empty;
            public Dictionary<string, string> Recommendations { get; set; } = new Dictionary<string, string>();
        }

        public Task<MetricsReport> GenerateReportAsync()
        {
            _metricsLock.Wait();
            try
            {
                var report = new MetricsReport
                {
                    Timestamp = DateTime.UtcNow,
                    Metrics = new Dictionary<string, MetricData>(),
                    Warnings = new List<string>(),
                    Recommendations = new Dictionary<string, string>()
                };

                // Analyze metrics
                AnalyzeMetrics(report);

                // Generate recommendations
                GenerateRecommendations(report);

                // Determine overall status
                report.OverallStatus = DetermineOverallStatus(report);

                return Task.FromResult(report);
            }
            finally
            {
                _metricsLock.Release();
            }
        }

        private void AnalyzeMetrics(MetricsReport report)
        {
            foreach (var metric in report.Metrics.Values)
            {
                switch (metric.Name)
                {
                    case "CpuUsage":
                        if (metric.Value > 80)
                        {
                            report.Warnings.Add($"High CPU usage detected: {metric.Value}%");
                        }
                        break;

                    case "MemoryUsage":
                        if (metric.Value > 1000)
                        {
                            report.Warnings.Add($"High memory usage detected: {metric.Value}MB");
                        }
                        break;

                    case "NetworkLatency":
                        if (metric.Value > 200)
                        {
                            report.Warnings.Add($"High network latency detected: {metric.Value}ms");
                        }
                        break;

                    case "PacketLoss":
                        if (metric.Value > 5)
                        {
                            report.Warnings.Add($"High packet loss detected: {metric.Value}%");
                        }
                        break;
                }
            }
        }

        private void GenerateRecommendations(MetricsReport report)
        {
            foreach (var warning in report.Warnings)
            {
                if (warning.Contains("CPU"))
                {
                    report.Recommendations["CPU"] = "Consider optimizing CPU-intensive operations";
                }
                else if (warning.Contains("memory"))
                {
                    report.Recommendations["Memory"] = "Review memory usage patterns and implement memory pooling";
                }
                else if (warning.Contains("latency"))
                {
                    report.Recommendations["Network"] = "Check network configuration and consider using a closer server";
                }
                else if (warning.Contains("packet loss", StringComparison.OrdinalIgnoreCase))
                {
                    report.Recommendations["Network"] = "Investigate network stability and consider using a wired connection";
                }
            }
        }

        private string DetermineOverallStatus(MetricsReport report)
        {
            if (report.Warnings.Count == 0)
            {
                return "Excellent";
            }
            else if (report.Warnings.Count <= 2)
            {
                return "Good";
            }
            else if (report.Warnings.Count <= 4)
            {
                return "Fair";
            }
            else
            {
                return "Poor";
            }
        }

        public Dictionary<string, MetricData> GetMetrics()
        {
            return _metrics.ToDictionary(kvp => kvp.Key, kvp => new MetricData(kvp.Key, kvp.Value.Average(mp => mp.Value), "unit"));
        }
    }

    public class NetworkStats
    {
        public double BytesReceived { get; set; }
        public double BytesSent { get; set; }
    }

    public class DiskStats
    {
        public double BytesRead { get; set; }
        public double BytesWritten { get; set; }
    }
}