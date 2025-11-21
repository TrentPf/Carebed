using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using System;

namespace Carebed.Models.Sensors
{
    /// <summary>
    /// Simulated EEG sensor. Returns a simple numeric metric (e.g., RMS amplitude) for demonstration.
    /// </summary>
    internal sealed class EegSensor : AbstractSensor
    {

        public EegSensor(string source, double min = 0.0, double max = 100.0, double criticalThreshold = 90.0)
            : base(source, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "uV"), ("Sensor", "EEG"));
            System.Guid correlationId = Guid.NewGuid();
            return new SensorData
            {
                Value = value,
                Source = SensorID,
                SensorType = SensorTypes.EEG,
                IsCritical = (value < _criticalThreshold),
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Metadata = meta
            };
        }
    }
}