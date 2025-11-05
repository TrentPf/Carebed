using Carebed.Infrastructure.EventBus;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.EventBus
{
    /// <summary>
    /// Defines the contract for an event bus that supports subscribing,
    /// unsubscribing, publishing, and lifecycle management of event messages.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes a handler to a specific event type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the event message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <param name="handler">The delegate to invoke when an event of type <typeparamref name="T"/> is published.</param>
        void Subscribe<T>(Action<T> handler) where T : IEventMessage;

        /// <summary>
        /// Unsubscribes a handler from a specific event type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the event message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <param name="handler">The delegate to remove from the subscriber registry.</param>
        void Unsubscribe<T>(Action<T> handler) where T : IEventMessage;

        /// <summary>
        /// Publishes an event message asynchronously to all subscribed handlers.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the event message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <param name="message">The event message instance to publish.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous publish operation.
        /// </returns>
        Task PublishAsync<T>(T message) where T : IEventMessage;

        /// <summary>
        /// Initializes the event bus.
        /// </summary>
        /// <remarks>
        /// Intended for setup tasks such as preloading subscriptions or configuring tracing hooks.
        /// </remarks>
        void Initialize();

        /// <summary>
        /// Shuts down the event bus and clears all subscriptions.
        /// </summary>
        /// <remarks>
        /// After calling this method, no handlers will remain registered.
        /// </remarks>
        void Shutdown();
    }
}


