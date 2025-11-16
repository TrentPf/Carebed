using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message
{
    public sealed record SensorData(
        double Value,
        string? Source = null,
        bool IsCritical = false,
        IReadOnlyDictionary<string, string>? Metadata = null
    ) : IEventMessage
    {
        /// <summary>
        /// Timestamp when the event message was created.
        /// </summary>
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Gets an (optional) unique identifier used to correlate related operations or requests.
        /// </summary>
        public Guid? CorrelationId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Gets the type of message represented by this instance.
        /// </summary>
        public MessageType MessageType { get; init; } = MessageType.SensorData;

        /// <summary>
        /// Optional source identifier associated with the data or event.
        /// </summary>
        ///// <remarks> This can be used to identify the origin of the data. </remarks>
        //string? IEventMessage.Source => Source;

        /// <summary>
        /// An optional dictionary for additional metadata
        /// </summary>
        /// <remarks> This can be used to store any extra information related to the event. </remarks>
        IReadOnlyDictionary<string, string>? IEventMessage.Metadata => Metadata;

        /// <summary>
        /// A flag indicating whether the message is critical.
        /// </summary>
        /// <remarks> This flag can be used to prioritize processing of critical messages. </remarks>
        bool IEventMessage.IsCritical => IsCritical;
    }
}
