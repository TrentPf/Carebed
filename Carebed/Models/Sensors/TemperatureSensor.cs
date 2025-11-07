csharp Carebed\Models\Sensors\TemperatureSensor.cs
using System;
using Carebed.Infrastructure.Message;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated temperature sensor.
    /// </summary>
    internal sealed class TemperatureSensor : AbstractSensor
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _criticalThreshold;

        public TemperatureSensor(string source, double min = 35.0, double max = 40.0, double criticalThreshold = 45.0)
            : base(source)
        {
            _min = min;
            _max = max;
            _criticalThreshold = criticalThreshold;
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "°C"), ("Sensor", "Temperature"));
            return new SensorData(value, Source, value >= _criticalThreshold, meta);
        }
    }
}