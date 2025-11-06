using Carebed.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.MessageEnvelope
{
    /// <summary>
    /// Defines the contract for an envelope that wraps event messages.
    /// Ensures consistency across modules and supports logging, debugging, and extensibility.
    /// </summary>
    public interface IMessageEnvelope
    {
        /// <summary>
        /// A unique identifier for the message envelope.
        /// </summary>
        Guid MessageId { get; }

        /// <summary>
        /// Timestamp when the message was created.
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// The origin of the message.
        /// </summary>
        MessageOriginEnum MessageOrigin { get; }
       
        /// <summary>
        /// Type of the message represented by this instance.
        /// </summary>
        MessageTypeEnum MessageType { get; }

        /// <summary>
        /// The actual payload of the message.
        /// </summary>
        object Payload { get; }

        /// <summary>
        /// A helper method to provide a string representation of the message envelope.
        /// </summary>
        /// <returns></returns>
        string ToString();
   }


}
