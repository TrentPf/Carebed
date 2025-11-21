using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
namespace Carebed.Models.Sensors
{
    /// <summary>
    /// Simulated temperature sensor.
    /// </summary>
    internal sealed class TemperatureSensor : AbstractSensor
    {
        public TemperatureSensor(string sensorID, SensorTypes sensorType = SensorTypes.Temperature, double min = 35.0, double max = 40.0, double criticalThreshold = 45.0)
            : base(sensorID, sensorType, min, max, criticalThreshold)
        {
        }

        public override SensorData ReadDataActual()
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