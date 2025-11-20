using System;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated blood oxygen (SpO2) sensor.
    /// </summary>
    internal sealed class BloodOxygenSensor : AbstractSensor
    {
        public BloodOxygenSensor(string source, double min = 85.0, double max = 100.0, double criticalThreshold = 90.0)
            : base(source, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "%"), ("Sensor", "SpO2"));
            return new SensorData(value, SensorID, value < _criticalThreshold, meta);
        }
    }
}