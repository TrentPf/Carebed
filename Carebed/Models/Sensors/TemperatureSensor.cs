using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Models.Sensors;
using System;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated temperature sensor.
    /// </summary>
    internal sealed class TemperatureSensor : AbstractSensor
    {
        public TemperatureSensor(string source, SensorTypes sensorType = SensorTypes.Temperature, double min = 35.0, double max = 40.0, double criticalThreshold = 45.0)
            : base(source, sensorType, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "°C"), ("Sensor", "Temperature"));           
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