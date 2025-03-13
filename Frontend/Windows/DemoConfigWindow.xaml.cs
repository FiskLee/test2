using ArmaReforgerServerMonitor.Frontend.Models;
using ArmaReforgerServerMonitor.Frontend.Services;
using Serilog;
using System;
using System.Windows;

namespace ArmaReforgerServerMonitor.Frontend.Windows
{
    /// <summary>
    /// Interaction logic for DemoConfigWindow.xaml
    /// </summary>
    /// <remarks>
    /// This window provides a UI for configuring demo mode parameters,
    /// allowing users to customize the behavior of sample data generation.
    /// </remarks>
    public partial class DemoConfigWindow : Window
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoConfigWindow>();
        private readonly IDemoService _demoService;
        private readonly DemoParameters _parameters;

        /// <summary>
        /// Initializes a new instance of the DemoConfigWindow class.
        /// </summary>
        /// <param name="demoService">Demo service instance</param>
        /// <param name="parameters">Demo parameters</param>
        public DemoConfigWindow(IDemoService demoService, DemoParameters parameters)
        {
            _logger.Verbose("Initializing DemoConfigWindow - DemoService: {ServiceType}, Parameters: {Parameters}",
                demoService?.GetType().Name ?? "null",
                parameters != null ? "present" : "null");

            ArgumentNullException.ThrowIfNull(demoService, nameof(demoService));
            ArgumentNullException.ThrowIfNull(parameters, nameof(parameters));

            InitializeComponent();
            _demoService = demoService;
            _parameters = new DemoParameters
            {
                CpuRange = parameters.CpuRange,
                MemoryRange = parameters.MemoryRange,
                FpsRange = parameters.FpsRange,
                PlayerCount = (Min: (int)parameters.PlayerCount.Min, Max: (int)parameters.PlayerCount.Max),
                LatencyRange = (Min: (int)parameters.LatencyRange.Min, Max: (int)parameters.LatencyRange.Max)
            };

            _logger.Verbose("Demo parameters initialized - CPU: {CpuMin}-{CpuMax}%, Memory: {MemMin}-{MemMax}%, FPS: {FpsMin}-{FpsMax}, Players: {PlayerMin}-{PlayerMax}, Latency: {LatMin}-{LatMax}ms",
                _parameters.CpuRange.Min,
                _parameters.CpuRange.Max,
                _parameters.MemoryRange.Min,
                _parameters.MemoryRange.Max,
                _parameters.FpsRange.Min,
                _parameters.FpsRange.Max,
                _parameters.PlayerCount.Min,
                _parameters.PlayerCount.Max,
                _parameters.LatencyRange.Min,
                _parameters.LatencyRange.Max);

            DataContext = new DemoConfigViewModel(_parameters);
            _logger.Verbose("DemoConfigWindow initialization complete");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Verbose("OK button clicked - Saving demo configuration");
            if (DataContext is not DemoConfigViewModel viewModel)
            {
                _logger.Error("DataContext is not DemoConfigViewModel");
                return;
            }

            _parameters.CpuRange = (Min: viewModel.CpuRangeMin, Max: viewModel.CpuRangeMax);
            _parameters.MemoryRange = (Min: viewModel.MemoryRangeMin, Max: viewModel.MemoryRangeMax);
            _parameters.FpsRange = (Min: viewModel.FpsRangeMin, Max: viewModel.FpsRangeMax);
            _parameters.PlayerCount = (Min: (int)viewModel.PlayerCountMin, Max: (int)viewModel.PlayerCountMax);
            _parameters.LatencyRange = (Min: (int)viewModel.LatencyRangeMin, Max: (int)viewModel.LatencyRangeMax);

            _logger.Verbose("Demo parameters updated - CPU: {CpuMin}-{CpuMax}%, Memory: {MemMin}-{MemMax}%, FPS: {FpsMin}-{FpsMax}, Players: {PlayerMin}-{PlayerMax}, Latency: {LatMin}-{LatMax}ms",
                _parameters.CpuRange.Min,
                _parameters.CpuRange.Max,
                _parameters.MemoryRange.Min,
                _parameters.MemoryRange.Max,
                _parameters.FpsRange.Min,
                _parameters.FpsRange.Max,
                _parameters.PlayerCount.Min,
                _parameters.PlayerCount.Max,
                _parameters.LatencyRange.Min,
                _parameters.LatencyRange.Max);

            DialogResult = true;
            Close();
            _logger.Verbose("DemoConfigWindow closed with OK result");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.Verbose("Cancel button clicked - Discarding demo configuration changes");
            DialogResult = false;
            Close();
            _logger.Verbose("DemoConfigWindow closed with Cancel result");
        }
    }

    internal class DemoConfigViewModel
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DemoConfigViewModel>();

        private double _cpuRangeMin;
        private double _cpuRangeMax;
        private double _memoryRangeMin;
        private double _memoryRangeMax;
        private double _fpsRangeMin;
        private double _fpsRangeMax;
        private int _playerCountMin;
        private int _playerCountMax;
        private bool _generateTrends;
        private double _trendCycleDuration;
        private bool _simulateLatency;
        private int _latencyRangeMin;
        private int _latencyRangeMax;
        private double _errorProbability;

        public double CpuRangeMin
        {
            get => _cpuRangeMin;
            set
            {
                _logger.Verbose("Updating CPU range minimum - Old: {OldValue}, New: {NewValue}", _cpuRangeMin, value);
                _cpuRangeMin = value;
            }
        }

        public double CpuRangeMax
        {
            get => _cpuRangeMax;
            set
            {
                _logger.Verbose("Updating CPU range maximum - Old: {OldValue}, New: {NewValue}", _cpuRangeMax, value);
                _cpuRangeMax = value;
            }
        }

        public double MemoryRangeMin
        {
            get => _memoryRangeMin;
            set
            {
                _logger.Verbose("Updating memory range minimum - Old: {OldValue}, New: {NewValue}", _memoryRangeMin, value);
                _memoryRangeMin = value;
            }
        }

        public double MemoryRangeMax
        {
            get => _memoryRangeMax;
            set
            {
                _logger.Verbose("Updating memory range maximum - Old: {OldValue}, New: {NewValue}", _memoryRangeMax, value);
                _memoryRangeMax = value;
            }
        }

        public double FpsRangeMin
        {
            get => _fpsRangeMin;
            set
            {
                _logger.Verbose("Updating FPS range minimum - Old: {OldValue}, New: {NewValue}", _fpsRangeMin, value);
                _fpsRangeMin = value;
            }
        }

        public double FpsRangeMax
        {
            get => _fpsRangeMax;
            set
            {
                _logger.Verbose("Updating FPS range maximum - Old: {OldValue}, New: {NewValue}", _fpsRangeMax, value);
                _fpsRangeMax = value;
            }
        }

        public int PlayerCountMin
        {
            get => _playerCountMin;
            set
            {
                _logger.Verbose("Updating player count minimum - Old: {OldValue}, New: {NewValue}", _playerCountMin, value);
                _playerCountMin = value;
            }
        }

        public int PlayerCountMax
        {
            get => _playerCountMax;
            set
            {
                _logger.Verbose("Updating player count maximum - Old: {OldValue}, New: {NewValue}", _playerCountMax, value);
                _playerCountMax = value;
            }
        }

        public bool GenerateTrends
        {
            get => _generateTrends;
            set
            {
                _logger.Verbose("Updating trend generation - Old: {OldValue}, New: {NewValue}", _generateTrends, value);
                _generateTrends = value;
            }
        }

        public double TrendCycleDuration
        {
            get => _trendCycleDuration;
            set
            {
                _logger.Verbose("Updating trend cycle duration - Old: {OldValue}, New: {NewValue}", _trendCycleDuration, value);
                _trendCycleDuration = value;
            }
        }

        public bool SimulateLatency
        {
            get => _simulateLatency;
            set
            {
                _logger.Verbose("Updating latency simulation - Old: {OldValue}, New: {NewValue}", _simulateLatency, value);
                _simulateLatency = value;
            }
        }

        public int LatencyRangeMin
        {
            get => _latencyRangeMin;
            set
            {
                _logger.Verbose("Updating latency range minimum - Old: {OldValue}, New: {NewValue}", _latencyRangeMin, value);
                _latencyRangeMin = value;
            }
        }

        public int LatencyRangeMax
        {
            get => _latencyRangeMax;
            set
            {
                _logger.Verbose("Updating latency range maximum - Old: {OldValue}, New: {NewValue}", _latencyRangeMax, value);
                _latencyRangeMax = value;
            }
        }

        public double ErrorProbability
        {
            get => _errorProbability;
            set
            {
                _logger.Verbose("Updating error probability - Old: {OldValue}, New: {NewValue}", _errorProbability, value);
                _errorProbability = value;
            }
        }

        public DemoConfigViewModel(DemoParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters, nameof(parameters));

            CpuRangeMin = parameters.CpuRange.Min;
            CpuRangeMax = parameters.CpuRange.Max;
            MemoryRangeMin = parameters.MemoryRange.Min;
            MemoryRangeMax = parameters.MemoryRange.Max;
            FpsRangeMin = parameters.FpsRange.Min;
            FpsRangeMax = parameters.FpsRange.Max;
            PlayerCountMin = (int)parameters.PlayerCount.Min;
            PlayerCountMax = (int)parameters.PlayerCount.Max;
            LatencyRangeMin = (int)parameters.LatencyRange.Min;
            LatencyRangeMax = (int)parameters.LatencyRange.Max;

            _logger.Verbose("DemoConfigViewModel initialized with parameters");
        }
    }
}