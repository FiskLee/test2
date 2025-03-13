using Serilog;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DemoMetrics
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoMetrics>();

        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double FPS { get; set; }
        public int PlayerCount { get; set; }

        public DemoMetrics()
        {
            _logger.Verbose("Initializing DemoMetrics with default values");
            LogMetrics();
        }

        private void LogMetrics()
        {
            _logger.Verbose("Current Metrics - CPU: {Cpu}%, Memory: {Memory}%, FPS: {Fps}, Players: {Players}",
                CpuUsage,
                MemoryUsage,
                FPS,
                PlayerCount);
        }
    }

    public class DemoMetricsEventArgs : EventArgs
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoMetricsEventArgs>();

        public DemoMetrics Metrics { get; }

        public DemoMetricsEventArgs(DemoMetrics metrics)
        {
            _logger.Verbose("Creating DemoMetricsEventArgs with new metrics");
            Metrics = metrics;
            LogMetrics();
        }

        private void LogMetrics()
        {
            _logger.Verbose("New Metrics - CPU: {Cpu}%, Memory: {Memory}%, FPS: {Fps}, Players: {Players}",
                Metrics.CpuUsage,
                Metrics.MemoryUsage,
                Metrics.FPS,
                Metrics.PlayerCount);
        }
    }

    public class DemoLogEventArgs : EventArgs
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoLogEventArgs>();

        public string Message { get; }
        public Serilog.Events.LogEventLevel Level { get; }

        public DemoLogEventArgs(string message, Serilog.Events.LogEventLevel level)
        {
            _logger.Verbose("Creating DemoLogEventArgs - Level: {Level}, Message: {Message}",
                level,
                message);
            Message = message;
            Level = level;
        }
    }
}