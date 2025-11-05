using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.EventBus
{
    /// <summary>
    /// Provides a base implementation of <see cref="IEventBus"/> that manages
    /// subscription and unsubscription of message handlers in a thread-safe manner.
    /// </summary>
    /// <remarks>
    /// This abstract class centralizes subscriber management logic and lifecycle
    /// operations. Concrete subclasses must implement <see cref="PublishAsync{}"/>
    /// to define how messages are dispatched to subscribers.
    /// </remarks>
    public abstract class AbstractEventBus : IEventBus
    {
        /// <summary>
        /// Internal registry of message subscribers, keyed by message type.
        /// </summary>
        /// <remarks>
        /// It is protected so that subclasses can access the subscriber data while maintaining encapsulation.
        /// </remarks>
        protected readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        /// <summary>
        /// Synchronization object used to ensure thread-safe access to the subscriber registry.
        /// </summary>
        protected readonly object _lock = new();

        /// <summary>
        /// Subscribes a handler to a specific message type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <param name="handler">The delegate to invoke when a message of type <typeparamref name="T"/> is published.</param>
        /// <remarks>
        /// Multiple handlers can be registered for the same message type.
        /// Thread-safe: access to the subscriber registry is protected by a lock.
        /// </remarks>
        public virtual void Subscribe<T>(Action<T> handler) where T : IEventMessage
        {
            var messageType = typeof(T);
            lock (_lock)
            {
                if (!_subscribers.ContainsKey(messageType))
                {
                    _subscribers[messageType] = new List<Delegate>();
                }
                _subscribers[messageType].Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribes a handler from a specific event type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <param name="handler">The delegate to remove from the subscriber registry.</param>
        /// <remarks>
        /// If the handler is not found, no action is taken.
        /// When the last handler for a given message type is removed, the type entry is cleared.
        /// Thread-safe: access to the subscriber registry is protected by a lock.
        /// </remarks>
        public virtual void Unsubscribe<T>(Action<T> handler) where T : IEventMessage
        {
            var messageType = typeof(T);
            lock (_lock)
            {
                if (_subscribers.TryGetValue(messageType, out var handlers))
                {
                    handlers.Remove(handler);
                    if (handlers.Count == 0)
                    {
                        _subscribers.Remove(messageType);
                    }
                }
            }
        }

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
        /// <remarks>
        /// Concrete subclasses must implement this method to define the dispatch strategy
        /// (e.g., synchronous, asynchronous, queued, or distributed).
        /// </remarks>
        public abstract Task PublishAsync<T>(T message) where T : IEventMessage;

        /// <summary>
        /// Initializes the event bus.
        /// </summary>
        /// <remarks>
        /// This method can be overridden by subclasses to preload subscriptions,
        /// configure tracing hooks, or perform other startup tasks.
        /// </remarks>
        public virtual void Initialize()
        {
            // Optional: preload subscriptions or setup tracing hooks
        }

        /// <summary>
        /// Shuts down the event bus and clears all subscriptions.
        /// </summary>
        /// <remarks>
        /// After calling this method, no handlers will remain registered.
        /// Thread-safe: access to the subscriber registry is protected by a lock.
        /// </remarks>
        public virtual void Shutdown()
        {
            lock (_lock)
            {
                _subscribers.Clear();
            }
        }

        /// <summary>
        /// Retrieves all handlers subscribed to the specified event type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the event message, constrained to <see cref="IEventMessage"/>.
        /// </typeparam>
        /// <returns>
        /// A list of delegates representing the handlers for the given event type.
        /// </returns>
        /// <remarks>
        /// This helper method is intended for use by subclasses when implementing
        /// <see cref="PublishAsync{T}"/>. It ensures consistent handler resolution
        /// across different event bus implementations.
        /// </remarks>
        protected List<Action<T>> GetHandlersFor<T>() where T : IEventMessage
        {
            var messageType = typeof(T);
            var handlersToInvoke = new List<Action<T>>();

            lock (_lock)
            {
                foreach (var entry in _subscribers)
                {
                    if (entry.Key.IsAssignableFrom(messageType))
                    {
                        handlersToInvoke.AddRange(entry.Value.Cast<Action<T>>());
                    }
                }
            }

            return handlersToInvoke;
        }
    }

}
