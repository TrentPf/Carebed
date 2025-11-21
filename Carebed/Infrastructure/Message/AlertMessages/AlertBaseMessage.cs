using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.AlertMessages
{
    public abstract class AlertBaseMessage<TPayload>: IEventMessage
    {
        /// <summary>
        /// The alert source.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Represents the timestamp when the alert was created.
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>
        /// Represents a unique identifier to correlate related alert messages.
        /// </summary>
        public Guid CorrelationId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// A flag indicating whether the alert is critical.
        /// </summary>
        public bool IsCritical => true;

        /// <summary>
        /// Represents the alert text or description.
        /// </summary>
        public string AlertText { get; set; } = string.Empty;

        /// <summary>
        /// Represents the type of the alert message.
        /// </summary>
        public MessageTypes MessageType => MessageTypes.AlertAction;
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Represents the payload of the alert message.
        /// </summary>
        public TPayload? Payload { get; set; } // Optional: for domain-specific data
    }
}
