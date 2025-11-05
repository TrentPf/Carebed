using Carebed.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public MessageOriginEnum MessageOrigin { get; }

        /// <summary>
        /// Message type of the payload.
        /// </summary>
        public MessageTypeEnum MessageType { get; }

        /// <summary>
        /// The actual payload of the message.
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// This constructor initializes a new instance of the MessageEnvelope class.
        /// </summary>
        /// <param name="payload">The payload of the message.</param>
        /// <param name="origin">The origin of the message.</param>
        /// <param name="type">The type of the message.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public MessageEnvelope(
            T payload,
            MessageOriginEnum origin,
            MessageTypeEnum type = MessageTypeEnum.Undefined)
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
