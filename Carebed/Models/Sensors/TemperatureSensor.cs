using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using System;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated temperature sensor.
    /// </summary>
    internal sealed class TemperatureSensor : AbstractSensor
    {
        public TemperatureSensor(string source, double min = 35.0, double max = 40.0, double criticalThreshold = 45.0)
            : base(source, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "°C"), ("Sensor", "Temperature"));
            return new SensorData(value, SensorID, value >= _criticalThreshold, meta);
        }
    }
}