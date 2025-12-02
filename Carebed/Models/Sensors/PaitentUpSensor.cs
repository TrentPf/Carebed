using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using System;
using System.Collections.Generic;

namespace Carebed.Models.Sensors
{
    /// <summary>
    /// Simulated Patient Up Sensor, which detects if a patient is in bed based on pressure.
    /// </summary>
    internal class PaitentUpSensor : AbstractSensor
    {
        public PaitentUpSensor(string sensorID, int min = 0, int max = 200, double criticalThreshold = 20.0)
            : base(sensorID, SensorTypes.PaitentUp, min, max, criticalThreshold)
        {
        }


        public override SensorData ReadDataActual()
        {
            // Simulate that the patient is in bed 80% of the time.
            bool isPatientInBed = Random.Shared.Next(0, 10) > 1;

            int value;
            if (isPatientInBed)
            {
                // Generate a pressure value indicating the patient is in bed.
                // The value should be between the critical threshold and the maximum.
                value = Random.Shared.Next((int)_criticalThreshold + 1, (int)_max);
            }
            else
            {
                // Generate a pressure value indicating the patient is out of bed.
                // The value should be between the minimum and the critical threshold.
                value = Random.Shared.Next((int)_min, (int)_criticalThreshold);
            }

            // A critical event occurs if the patient is up (pressure is below the threshold).
            bool isCritical = value < _criticalThreshold;

            var metadata = BuildMetadata(("Unit", "lbs"), ("Sensor", "BedPressure"));
            System.Guid correlationId = Guid.NewGuid();
            return new SensorData
            {
                Value = value,
                Source = SensorID,
                SensorType = this.SensorType,
                IsCritical = isCritical,
                CreatedAt = DateTime.UtcNow,
                CorrelationId = correlationId,
                Metadata = metadata,

            };
        }
    }
}