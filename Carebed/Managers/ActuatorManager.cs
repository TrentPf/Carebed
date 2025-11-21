using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Models.Actuators;
using System;

namespace Carebed.Managers
{
    public class ActuatorManager : IManager
    {
        private readonly IEventBus _eventBus;

        private Dictionary<string, IActuator> _actuators = new();

        /// <summary>
        /// A dictionary mapping actuators to their state change handlers.
        /// </summary>
        private readonly Dictionary<IActuator, Action<ActuatorStates>> _stateChangedHandlers = new();

        public ActuatorManager(IEventBus eventBus, IEnumerable<IActuator> actuators)
        {
            _eventBus = eventBus;

            // Future improvement: Check for duplicate actuator IDs and handle accordingly.
            foreach (var actuator in actuators)
            {
                Action<ActuatorStates> handler = state => HandleStateChanged(actuator, state);
                _stateChangedHandlers[actuator] = handler;
                actuator.OnStateChanged += handler;
                _actuators[actuator.ActuatorId] = actuator;
            }

            // Emit the initial inventory message
            EmitActuatorInventoryMessage();
        }

        /// <summary>
        /// Handles the state change event from an actuator and publishes the appropriate message to the event bus.
        /// </summary>
        /// <param name="actuator"></param>
        /// <param name="state"></param>
        private async void HandleStateChanged(IActuator actuator, ActuatorStates state)
        {
            //var telemetry = actuator.GetTelemetry();

            if (state == ActuatorStates.Error)
            {
                // Publish an error message if the actuator enters the Error state
                var errorMsg = new ActuatorErrorMessage
                {
                    ActuatorId = actuator.ActuatorId,
                    TypeOfActuator = actuator.Type,
                    CurrentState = state,
                    ErrorCode = "ERROR",
                    Description = "Actuator encountered an error."
                };

                var errorEnvelope = new MessageEnvelope<ActuatorErrorMessage>(
                    errorMsg,
                    MessageOrigins.ActuatorManager,
                    MessageTypes.ActuatorError
                );

                // Publish the error message asynchronously to the event bus
                await _eventBus.PublishAsync(errorEnvelope);
            }
            else
            {
                var statusUpdate = new ActuatorStatusMessage
                {
                    ActuatorId = actuator.ActuatorId,
                    TypeOfActuator = actuator.Type,
                    CurrentState = state
                };

                var statusEnvelope = new MessageEnvelope<ActuatorStatusMessage>(
                    statusUpdate,
                    MessageOrigins.ActuatorManager,
                    MessageTypes.ActuatorStatus
                );

                await _eventBus.PublishAsync(statusEnvelope);
            }
        }


        /// <summary>
        /// Handles incoming ActuatorCommandMessage events.
        /// </summary>
        /// <param name="envelope"></param>
        private async void HandleActuatorCommand(MessageEnvelope<ActuatorCommandMessage> envelope)
        {
            var commandMessage = envelope.Payload;

            // Look through the list of actuators to find the desired one, and attempt to execute the command.
            if (_actuators.TryGetValue(commandMessage.ActuatorId, out var actuator))
            {
                // Actuator found - try to execute the command
                bool success = actuator.TryExecute(commandMessage.CommandType);

                // Publish acknowledgment based on execution result
                if (success)
                {
                    await PublishCommandAckAsync(commandMessage, canExecute: true); // Positive ack
                }
                else
                {
                    await PublishCommandAckAsync(commandMessage, canExecute: false); // Negative ack
                }                
                
            }
            else
            {
                // Actuator not found, publish negative ack
                await PublishCommandAckAsync(commandMessage, canExecute: false); // Negative ack
            }
        }

        /// <summary>
        /// Publishes an ActuatorCommandAckMessage to the event bus.
        /// Used to acknowledge receipt and execution capability of actuator commands.
        /// </summary>
        private async Task PublishCommandAckAsync(ActuatorCommandMessage actuatorCommandMessage, bool canExecute)
        {
            var ackMessage = new ActuatorCommandAckMessage
            {
                ActuatorId = actuatorCommandMessage.ActuatorId,
                TypeOfActuator = actuatorCommandMessage.TypeOfActuator,
                CommandType = actuatorCommandMessage.CommandType,
                CorrelationId = actuatorCommandMessage.CorrelationId,
                CanExecuteCommand = canExecute
            };

            var envelope = new MessageEnvelope<ActuatorCommandAckMessage>(
                ackMessage,
                MessageOrigins.ActuatorManager,
                MessageTypes.ActuatorCommandAck
            );

            await _eventBus.PublishAsync(envelope);
        }

        /// <summary>
        /// Stops the ActuatorManager by unsubscribing from ActuatorCommandMessage events.
        /// </summary>
        public void Stop()
        {
            _eventBus.Unsubscribe<ActuatorCommandMessage>(HandleActuatorCommand);
        }

        /// <summary>
        /// Starts the ActuatorManager by subscribing to ActuatorCommandMessage events.
        /// </summary>
        public void Start()
        {
            _eventBus.Subscribe<ActuatorCommandMessage>(HandleActuatorCommand);
        }

        /// <summary>
        /// Emits an ActuatorInventoryMessage containing the list of managed actuators and their types.
        /// </summary>
        public void EmitActuatorInventoryMessage()
        {
            var metadata = new Dictionary<string, string>();

            foreach(var actuator in _actuators.Values)
            {
                metadata[actuator.ActuatorId] = actuator.Type.ToString();
            }

            var inventoryMessage = new ActuatorInventoryMessage
            {
                ActuatorId = "ActuatorManager",
                TypeOfActuator = ActuatorTypes.Manager,
                Metadata = metadata
            };
            var envelope = new MessageEnvelope<ActuatorInventoryMessage>(
                inventoryMessage,
                MessageOrigins.ActuatorManager,
                MessageTypes.ActuatorInventory
            );

            // Fire-and-forget the async publish; BasicEventBus executes handlers on thread-pool.
            _ = _eventBus.PublishAsync(envelope);

        }

        /// <summary>
        /// Disposes the ActuatorManager by unsubscribing from all actuator state change events.
        /// </summary>
        public void Dispose()
        {
            // Iterate through the actuators and unsubscribe from their state change events
            foreach (var item in _stateChangedHandlers)
                item.Key.OnStateChanged -= item.Value;
            _stateChangedHandlers.Clear();
        }
    }
}
