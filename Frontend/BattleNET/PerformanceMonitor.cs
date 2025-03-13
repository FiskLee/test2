using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace BattleNET
{
    public class PerformanceMonitor : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private readonly ConcurrentDictionary<string, Stopwatch> _activeTimers;
        private readonly SemaphoreSlim _monitorLock;
        private readonly int _maxHistorySize;
        private bool _isDisposed;

        public PerformanceMonitor(Serilog.ILogger logger, int maxHistorySize = 100)
        {
            _logger = logger ?? Log.ForContext<PerformanceMonitor>();
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            _activeTimers = new ConcurrentDictionary<string, Stopwatch>();
            _monitorLock = new SemaphoreSlim(1, 1);
            _maxHistorySize = maxHistorySize;
        }

        public class PerformanceMetric
        {
            public string Name { get; }
            public double Value { get; private set; }
            public string Unit { get; }
            public DateTime Timestamp { get; private set; }
            public List<double> History { get; }
            public double MinValue { get; private set; }
            public double MaxValue { get; private set; }
            public double AverageValue { get; private set; }

            public PerformanceMetric(string name, double value, string unit)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
                Value = value;
                Unit = unit ?? throw new ArgumentNullException(nameof(unit));
                Timestamp = DateTime.UtcNow;
                History = new List<double>();
                MinValue = value;
                MaxValue = value;
                AverageValue = value;
            }

            public void Update(double newValue)
            {
                History.Add(Value);
                if (History.Count > 100) // Keep last 100 measurements
                {
                    History.RemoveAt(0);
                }

                Value = newValue;
                Timestamp = DateTime.UtcNow;

                // Update statistics
                MinValue = Math.Min(MinValue, newValue);
                MaxValue = Math.Max(MaxValue, newValue);
                AverageValue = History.Count > 0 ? History.Average() : newValue;
            }
        }

        public class PerformanceReport
        {
            public DateTime Timestamp { get; }
            public Dictionary<string, PerformanceMetric> Metrics { get; set; }
            public Collection<string> Warnings { get; }
            public string OverallStatus { get; set; }
            public Dictionary<string, string> Recommendations { get; }

            public PerformanceReport()
            {
                Timestamp = DateTime.UtcNow;
                Metrics = new Dictionary<string, PerformanceMetric>();
                Warnings = new Collection<string>();
                Recommendations = new Dictionary<string, string>();
                OverallStatus = "Unknown"; // Initialize with a default value
            }
        }

        public void TrackMetric(string name, double value, string unit)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Metric name cannot be null or empty", nameof(name));
            }

            if (string.IsNullOrEmpty(unit))
            {
                throw new ArgumentException("Unit cannot be null or empty", nameof(unit));
            }

            _metrics.AddOrUpdate(
                name,
                new PerformanceMetric(name, value, unit),
                (_, metric) =>
                {
                    metric.Update(value);
                    return metric;
                });
        }

        public void StartTimer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Timer name cannot be null or empty", nameof(name));
            }

            var timer = new Stopwatch();
            timer.Start();
            _activeTimers.TryAdd(name, timer);
        }

        public void StopTimer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Timer name cannot be null or empty", nameof(name));
            }

            if (_activeTimers.TryRemove(name, out var timer))
            {
                timer.Stop();
                TrackMetric(name, timer.ElapsedMilliseconds, "ms");
            }
        }

        public async Task<PerformanceReport> GenerateReportAsync()
        {
            await _monitorLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var report = new PerformanceReport
                {
                    Metrics = new Dictionary<string, PerformanceMetric>(_metrics)
                };

                // Analyze metrics
                AnalyzeMetrics(report);

                // Generate recommendations
                GenerateRecommendations(report);

                // Determine overall status
                report.OverallStatus = DetermineOverallStatus(report);

                return report;
            }
            finally
            {
                _monitorLock.Release();
            }
        }

        public static void AnalyzeMetrics(PerformanceReport report)
        {
            foreach (var metric in report.Metrics.Values)
            {
                // Check for high latency
                if (metric.Name.Contains("Latency", StringComparison.OrdinalIgnoreCase) && metric.Value > 200)
                {
                    report.Warnings.Add($"High latency detected: {metric.Value}ms");
                }

                // Check for high memory usage
                if (metric.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) && metric.Value > 1000)
                {
                    report.Warnings.Add($"High memory usage detected: {metric.Value}MB");
                }

                // Check for high CPU usage
                if (metric.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) && metric.Value > 80)
                {
                    report.Warnings.Add($"High CPU usage detected: {metric.Value}%");
                }

                // Check for command execution time
                if (metric.Name.Contains("CommandExecution", StringComparison.OrdinalIgnoreCase) && metric.Value > 1000)
                {
                    report.Warnings.Add($"Slow command execution detected: {metric.Value}ms");
                }
            }
        }

        public static void GenerateRecommendations(PerformanceReport report)
        {
            foreach (var warning in report.Warnings)
            {
                if (warning.Contains("latency", StringComparison.OrdinalIgnoreCase))
                {
                    report.Recommendations["Network"] = "Consider optimizing network configuration or using a closer server";
                }
                else if (warning.Contains("memory", StringComparison.OrdinalIgnoreCase))
                {
                    report.Recommendations["Memory"] = "Review memory usage patterns and consider implementing memory pooling";
                }
                else if (warning.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                {
                    report.Recommendations["CPU"] = "Optimize CPU-intensive operations and consider implementing caching";
                }
                else if (warning.Contains("command execution", StringComparison.OrdinalIgnoreCase))
                {
                    report.Recommendations["Commands"] = "Review command execution patterns and optimize slow commands";
                }
            }
        }

        public static string DetermineOverallStatus(PerformanceReport report)
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

        public void TrackCommandExecution(string command, long executionTimeMs)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            TrackMetric($"Command_{command}", executionTimeMs, "ms");
        }

        public void TrackMemoryUsage(long memoryBytes)
        {
            TrackMetric("MemoryUsage", memoryBytes / (1024 * 1024), "MB");
        }

        public void TrackCpuUsage(double cpuPercentage)
        {
            TrackMetric("CpuUsage", cpuPercentage, "%");
        }

        public void TrackNetworkLatency(long latencyMs)
        {
            TrackMetric("NetworkLatency", latencyMs, "ms");
        }

        public void TrackPacketLoss(double packetLossPercentage)
        {
            TrackMetric("PacketLoss", packetLossPercentage, "%");
        }

        public void TrackCommandQueueSize(int queueSize)
        {
            TrackMetric("CommandQueueSize", queueSize, "commands");
        }

        public void TrackActiveConnections(int connectionCount)
        {
            TrackMetric("ActiveConnections", connectionCount, "connections");
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _monitorLock.Dispose();
                _metrics.Clear();
                _activeTimers.Clear();
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