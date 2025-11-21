using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed.Models.Sensors
{
    /// <summary>
    /// Simulated blood oxygen (SpO2) sensor.
    /// </summary>
    internal sealed class BloodOxygenSensor : AbstractSensor
    {
        public BloodOxygenSensor(string sensorID, SensorTypes sensorType = SensorTypes.BloodOxygen, double min = 85.0, double max = 100.0, double criticalThreshold = 90.0)
            : base(sensorID, sensorType, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadDataActual()
        {
            double value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "%"), ("Sensor", "SpO2"));
            System.Guid correlationId = Guid.NewGuid();
            
            return new SensorData
            {
                Value = value,
                Source = SensorID,
                SensorType = SensorTypes.BloodOxygen,
                IsCritical = (value < _criticalThreshold),
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Metadata = meta
            };
        }
    }
}