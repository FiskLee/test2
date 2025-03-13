using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class TelemetryData
    {
        public DateTime Timestamp { get; set; }
        public string EventName { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
    }

    public interface IDataCollector
    {
        Task<TelemetryData> CollectDataAsync();
        void StartCollecting();
        void StopCollecting();
    }

    public interface IDataExporter
    {
        Task ExportDataAsync(TelemetryData data);
        Task ExportBatchAsync(IEnumerable<TelemetryData> data);
    }
}