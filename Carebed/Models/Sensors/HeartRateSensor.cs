using System;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated heart rate sensor (beats per minute).
    /// </summary>
    internal sealed class HeartRateSensor : AbstractSensor
    {
        private readonly int _lowCritical;

        public HeartRateSensor(string source, int min = 40, int max = 130, int lowCritical = 40, int highCritical = 120)
            : base(source, min, max, highCritical)
        {
            _lowCritical = lowCritical;
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.Next((Int32)_min, (Int32)_max + 1);
            var isCritical = value < _lowCritical || value > _criticalThreshold;
            var meta = BuildMetadata(("Unit", "bpm"), ("Sensor", "HeartRate"));
            return new SensorData(value, SensorID, isCritical, meta);
        }
    }
}