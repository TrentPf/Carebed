using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using System;

namespace Carebed.Models.Sensors
{
    /// <summary>
    /// Simulated heart rate sensor (beats per minute).
    /// </summary>
    internal sealed class HeartRateSensor : AbstractSensor
    {
        private readonly int _lowCritical;

        public HeartRateSensor(string sensorID, SensorTypes sensorType = SensorTypes.HeartRate, int min = 40, int max = 130, int lowCritical = 40, int highCritical = 120)
            : base(sensorID, sensorType, min, max, highCritical)
        {
            _lowCritical = lowCritical;
        }

        public override SensorData ReadDataActual()
        {
            var value = Random.Shared.Next((Int32)_min, (Int32)_max + 1);
            var isCritical = value < _lowCritical || value > _criticalThreshold;
            var meta = BuildMetadata(("Unit", "bpm"), ("Sensor", "HeartRate"));
            System.Guid correlationId = Guid.NewGuid();
            return new SensorData
            {
                Value = value,
                Source = SensorID,
                SensorType = this.SensorType,
                IsCritical = (value < _criticalThreshold),
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Metadata = meta
            };
        }
    }
}