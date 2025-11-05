using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.EventBus
{
    /// <summary>
    /// A bare-bones in-memory implementation of <see cref="AbstractEventBus"/>.
    /// Provides synchronous, inline dispatch of event messages.
    /// Useful for quick testing and regression scenarios.
    /// </summary>
    public class BasicEventBus : AbstractEventBus
    {
        /// <summary>
        /// Subscribes a handler to a specific event type.
        /// </summary>
        public override void Subscribe<T>(Action<T> handler) where T : IEventMessage
        {
            base.Subscribe(handler);
        }

        /// <summary>
        /// Unsubscribes a handler from a specific event type.
        /// </summary>
        public override void Unsubscribe<T>(Action<T> handler) where T : IEventMessage
        {
            base.Unsubscribe(handler);
        }

        /// <summary>
        /// Publishes an event message synchronously to all subscribed handlers.
        /// </summary>
        public override Task PublishAsync<T>(T message) where T : IEventMessage
        {
            var handlers = GetHandlersFor<T>();

            foreach (var handler in handlers)
            {
                handler(message); // synchronous call
            }

            return Task.CompletedTask; // satisfy async contract
        }

        /// <summary>
        /// Initializes the event bus (no-op for BasicEventBus).
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// Shuts down the event bus and clears all subscriptions.
        /// </summary>
        public override void Shutdown()
        {
            base.Shutdown();
        }
    }
}
