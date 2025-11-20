using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    /// <summary>
    /// Encapsulates a sensor data reading, including its value, source, criticality, and metadata.
    /// </summary>
    public record SensorData<TValue>
    {
        // The measured value from the sensor (e.g., temperature, heart rate)
        public required TValue Value { get; init; }

        // The unique identifier or logical name of the sensor (e.g., "tempSensor1", "RoomA_EEG")
        public required string Source { get; init; }

        // The type of sensor (e.g., Temperature, HeartRate, EEG)
        public required SensorType SensorType { get; init; }

        // Indicates if the reading is considered critical (e.g., out of safe range)
        public required bool IsCritical { get; init; }

        // Timestamp when the reading was taken
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        // Optional: Unique identifier for correlating related messages/events
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        // Optional: Additional metadata (units, calibration info, etc.)
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        // Optional: Sensor state at the time of reading (if relevant)
        public SensorState? State { get; init; }
    }
}
