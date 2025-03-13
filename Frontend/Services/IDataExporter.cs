using System.Threading.Tasks;
using System.Collections.Generic;
using ArmaReforgerServerMonitor.Frontend.Models;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for exporting telemetry data.
    /// </summary>
    public interface IDataExporter
    {
        /// <summary>
        /// Exports the collected data in the specified format.
        /// </summary>
        /// <param name="data">The data to export.</param>
        /// <param name="format">The format to export the data in.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ExportAsync(IEnumerable<TelemetryData> data, string format);
    }
} 