using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.EventBus
{
    /// <summary>
    /// A concrete implementation of IMessageEnvelope that wraps a strongly typed payload.
    /// </summary>
    /// <remarks>
    /// This class provides a standardized way to encapsulate messages with metadata.
    /// </remarks>
    public class MessageEnvelope<T> : IMessageEnvelope
    {
        /// <summary>
        /// Unique identifier for the message envelope.
        /// </summary>
        public Guid MessageId { get; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.UtcNow;

        /// <summary>
        /// Origin of the message.
        /// </summary>
        public MessageOrigin MessageOrigin { get; }

        /// <summary>
        /// Message type of the payload.
        /// </summary>
        public MessageType MessageType { get; }

        /// <summary>
        /// The actual payload of the message.
        /// </summary>
        public T Payload { get; }

        /// <summary>
        /// Provides the payload as an object for the IMessageEnvelope interface. With this, 
        /// the generic payload can be accessed in a non-generic context. That means we 
        /// can access it like a IMessageEnvelope if we don't know the type T at compile time.<br/>
        /// <br/>Otherwise one can access the strongly typed Payload property.
        /// </summary>
        object IMessageEnvelope.Payload => Payload!;

        /// <summary>
        /// This constructor initializes a new instance of the MessageEnvelope class.
        /// </summary>
        /// <param name="payload">The payload of the message.</param>
        /// <param name="origin">The origin of the message.</param>
        /// <param name="type">The type of the message.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public MessageEnvelope(
            T payload,
            MessageOrigin origin,
            MessageType type = MessageType.Undefined)
        {
            // Validate and assign properties

            // Do not allow null payloads
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));

            // Assign origin and type
            MessageOrigin = origin;
            MessageType = type;
        }

        /// <summary>
        /// Helper method to provide a string representation of the message envelope.
        /// </summary>
        /// <returns> A string representation of the message envelope. </returns>
        public override string ToString()
        {
            return $"[{Timestamp:O}] {MessageOrigin}::{MessageType} " +
                   $"(Id={MessageId}, PayloadType={typeof(T).Name})";
        }
    }
}
