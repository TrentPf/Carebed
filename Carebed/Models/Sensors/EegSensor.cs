csharp Carebed\Models\Sensors\EegSensor.cs
using System;
using Carebed.Infrastructure.Message;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Simulated EEG sensor. Returns a simple numeric metric (e.g., RMS amplitude) for demonstration.
    /// </summary>
    internal sealed class EegSensor : AbstractSensor
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _criticalThreshold;

        public EegSensor(string source, double min = 0.0, double max = 100.0, double criticalThreshold = 90.0)
            : base(source)
        {
            _min = min;
            _max = max;
            _criticalThreshold = criticalThreshold;
        }

        public override SensorData ReadData()
        {
            var value = Random.Shared.NextDouble() * (_max - _min) + _min;
            var meta = BuildMetadata(("Unit", "uV"), ("Sensor", "EEG"));
            return new SensorData(value, Source, value >= _criticalThreshold, meta);
        }
    }
}