using Serilog;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Parameters for customizing demo data generation.
    /// </summary>
    public class DemoParameters
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoParameters>();

        /// <summary>
        /// CPU usage range (percentage).
        /// </summary>
        public (double Min, double Max) CpuRange { get; set; } = (20, 80);

        /// <summary>
        /// Memory usage range (percentage).
        /// </summary>
        public (double Min, double Max) MemoryRange { get; set; } = (30, 70);

        /// <summary>
        /// FPS range for server performance.
        /// </summary>
        public (double Min, double Max) FpsRange { get; set; } = (30, 60);

        /// <summary>
        /// Number of simulated players.
        /// </summary>
        public (int Min, int Max) PlayerCount { get; set; } = (5, 20);

        /// <summary>
        /// Frequency of data updates in milliseconds.
        /// </summary>
        public int UpdateFrequencyMs { get; set; } = 1000;

        /// <summary>
        /// Probability of generating error events (0-1).
        /// </summary>
        public double ErrorProbability { get; set; } = 0.05;

        /// <summary>
        /// Whether to simulate network latency.
        /// </summary>
        public bool SimulateLatency { get; set; } = false;

        /// <summary>
        /// Network latency range in milliseconds.
        /// </summary>
        public (int Min, int Max) LatencyRange { get; set; } = (50, 200);

        /// <summary>
        /// Whether to generate trending data.
        /// </summary>
        public bool GenerateTrends { get; set; } = true;

        /// <summary>
        /// Duration of trend cycles in seconds.
        /// </summary>
        public int TrendCycleDuration { get; set; } = 300;

        public DemoParameters()
        {
            _logger.Verbose("Initializing DemoParameters with default values");
            LogParameters();
        }

        private void LogParameters()
        {
            _logger.Verbose("Performance Ranges - CPU: {CpuMin}-{CpuMax}%, Memory: {MemMin}-{MemMax}%, FPS: {FpsMin}-{FpsMax}",
                CpuRange.Min,
                CpuRange.Max,
                MemoryRange.Min,
                MemoryRange.Max,
                FpsRange.Min,
                FpsRange.Max);

            _logger.Verbose("Player Settings - Count: {PlayerMin}-{PlayerMax}, Update Frequency: {Freq}ms",
                PlayerCount.Min,
                PlayerCount.Max,
                UpdateFrequencyMs);

            _logger.Verbose("Error Settings - Probability: {Error}, Simulate Latency: {Latency}",
                ErrorProbability,
                SimulateLatency);

            if (SimulateLatency)
            {
                _logger.Verbose("Latency Range: {LatencyMin}-{LatencyMax}ms",
                    LatencyRange.Min,
                    LatencyRange.Max);
            }

            _logger.Verbose("Trend Settings - Generate: {Generate}, Cycle Duration: {Duration}s",
                GenerateTrends,
                TrendCycleDuration);
        }
    }

    /// <summary>
    /// Event arguments for demo parameter changes.
    /// </summary>
    public class DemoParametersChangedEventArgs : EventArgs
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoParametersChangedEventArgs>();

        /// <summary>
        /// The new parameters that were set.
        /// </summary>
        public DemoParameters Parameters { get; }

        /// <summary>
        /// Creates a new DemoParametersChangedEventArgs instance.
        /// </summary>
        /// <param name="parameters">The updated parameters</param>
        public DemoParametersChangedEventArgs(DemoParameters parameters)
        {
            _logger.Verbose("Creating DemoParametersChangedEventArgs with new parameters");
            Parameters = parameters;
            LogParameters();
        }

        private void LogParameters()
        {
            _logger.Verbose("New Parameters - CPU: {CpuMin}-{CpuMax}%, Memory: {MemMin}-{MemMax}%, FPS: {FpsMin}-{FpsMax}",
                Parameters.CpuRange.Min,
                Parameters.CpuRange.Max,
                Parameters.MemoryRange.Min,
                Parameters.MemoryRange.Max,
                Parameters.FpsRange.Min,
                Parameters.FpsRange.Max);

            _logger.Verbose("Player Settings - Count: {PlayerMin}-{PlayerMax}, Update Frequency: {Freq}ms",
                Parameters.PlayerCount.Min,
                Parameters.PlayerCount.Max,
                Parameters.UpdateFrequencyMs);

            _logger.Verbose("Error Settings - Probability: {Error}, Simulate Latency: {Latency}",
                Parameters.ErrorProbability,
                Parameters.SimulateLatency);

            if (Parameters.SimulateLatency)
            {
                _logger.Verbose("Latency Range: {LatencyMin}-{LatencyMax}ms",
                    Parameters.LatencyRange.Min,
                    Parameters.LatencyRange.Max);
            }

            _logger.Verbose("Trend Settings - Generate: {Generate}, Cycle Duration: {Duration}s",
                Parameters.GenerateTrends,
                Parameters.TrendCycleDuration);
        }
    }
}