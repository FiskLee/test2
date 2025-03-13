using System;
using System.Collections.Generic;

namespace BattleNET
{
    public class NetworkMeasurement
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
        public List<MeasurementPoint> History { get; set; }

        public NetworkMeasurement(string name, double value, string unit)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Timestamp = DateTime.UtcNow;
            History = new List<MeasurementPoint>();
        }

        public void AddToHistory(double value)
        {
            History.Add(new MeasurementPoint(value, DateTime.UtcNow));
            if (History.Count > 100) // Keep last 100 measurements
            {
                History.RemoveAt(0);
            }
        }
    }

    public class MeasurementPoint
    {
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }

        public MeasurementPoint(double value, DateTime timestamp)
        {
            Value = value;
            Timestamp = timestamp;
        }
    }
}