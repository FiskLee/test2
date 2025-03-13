using System.Threading.Tasks;
using System.Collections.Generic;
using ArmaReforgerServerMonitor.Frontend.Models;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for collecting telemetry data.
    /// </summary>
    public interface IDataCollector
    {
        /// <summary>
        /// Starts the data collection process.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the data collection process.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Retrieves the collected data.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation, containing the collected data.</returns>
        Task<IEnumerable<TelemetryData>> GetDataAsync();
    }
} 