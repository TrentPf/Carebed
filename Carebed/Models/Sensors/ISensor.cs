using System;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed.Domain.Sensors
{
    /// <summary>
    /// Minimal sensor contract used by the application composition root.
    /// Implementations return a <see cref="SensorData"/> snapshot when polled.
    /// </summary>
    public interface ISensor
    {
        /// <summary>
        /// Logical source/id for the sensor (e.g. "Room A").
        /// </summary>
        string SensorID { get; init; }

        /// <summary>
        /// Read a single snapshot of sensor data. This is called by the SensorManager every poll cycle.
        /// </summary>
        /// <returns>A <see cref="SensorData"/> payload representing the current reading.</returns>
        SensorData ReadData();

        /// <summary>
        /// Start periodic sampling if the sensor implements its own timer (optional).
        /// SensorManager will call this when it is started.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop periodic sampling (optional).
        /// </summary>
        void Stop();
    }
}
