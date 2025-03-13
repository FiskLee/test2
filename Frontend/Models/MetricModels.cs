using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class MetricValue
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public Dictionary<string, string> Tags { get; set; } = new();
        public string Name { get; set; } = string.Empty;
    }

    public interface IMetricCollector
    {
        Task<MetricValue> CollectMetricAsync(string metricName);
        Task<IEnumerable<MetricValue>> CollectMetricsAsync(IEnumerable<string> metricNames);
    }
}