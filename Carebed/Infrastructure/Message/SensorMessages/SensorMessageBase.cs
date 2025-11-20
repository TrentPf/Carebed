using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    public abstract class SensorMessageBase : IEventMessage
    {
        /// <summary>
        /// The unique identifier of the sensor involved in this message.
        /// </summary>
        public required string SensorID { get; set; }

        /// <summary>
        /// The type of sensor (e.g., Temperature, Pressure, HeartRate).
        /// </summary>
        public required SensorType TypeOfSensor { get; set; }

        /// <summary>
        /// Timestamp of when the message was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// A required unique identifier to correlate related messages.
        /// </summary>
        public Guid CorrelationId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// An optional dictionary for additional metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// A flag indicating whether the message is critical.
        /// </summary>
        public bool IsCritical { get; set; } = false;

        public override string ToString()
        {
            return $"SensorMessageBase: SensorID={SensorID}, TypeOfSensor={TypeOfSensor}, CreatedAt={CreatedAt}, CorrelationId={CorrelationId}, IsCritical={IsCritical}";
        }
    }
}
