using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message
{
    /// <summary>
    /// Represents a message in the event system.
    /// </summary>
    public interface IEventMessage
    {
        /// <summary>
        /// Used as a timestamp for when the event was created
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Correlation ID to link related messages together
        /// </summary>
        Guid CorrelationId { get; }

        /// <summary>
        /// Describes the message type (e.g. Info, Warning, Error)
        /// </summary>
        MessageTypeEnum MessageType { get; }

        /// <summary>
        /// Gets the source identifier associated with the data or event, e.g. "Sensor:temperature-1".
        /// </summary>
        string? Source { get; }                 

        /// <summary>
        /// An optional dictionary for additional metadata
        /// </summary>
        IReadOnlyDictionary<string, string>? Metadata { get; }

        /// <summary>
        /// Optional: a small convenience to indicate importance
        /// </summary>
        bool IsCritical { get; }
    }
}
