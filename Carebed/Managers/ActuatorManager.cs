using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Models.Actuators;

namespace Carebed.Managers
{
    public class ActuatorManager : IManager
    {
        private readonly IEventBus _eventBus;

        private Dictionary<string, IActuator> _actuators = new();

        /// <summary>
        /// A dictionary mapping actuators to their state change handlers.
        /// </summary>
        private readonly Dictionary<IActuator, Action<ActuatorState>> _stateChangedHandlers = new();

        public ActuatorManager(IEventBus eventBus, IEnumerable<IActuator> actuators)
        {
            _eventBus = eventBus;

            // Future improvement: Check for duplicate actuator IDs and handle accordingly.
            foreach (var actuator in actuators)
            {
                Action<ActuatorState> handler = state => HandleStateChanged(actuator, state);
                _stateChangedHandlers[actuator] = handler;
                actuator.OnStateChanged += handler;
                _actuators[actuator.ActuatorId] = actuator;
            }
        }

        /// <summary>
        /// Handles the state change event from an actuator and publishes the appropriate message to the event bus.
        /// </summary>
        /// <param name="actuator"></param>
        /// <param name="state"></param>
        private async void HandleStateChanged(IActuator actuator, ActuatorState state)
        {
            //var telemetry = actuator.GetTelemetry();

            if (state == ActuatorState.Error)
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
                    MessageOrigin.ActuatorManager,
                    MessageType.ActuatorError
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
                    MessageOrigin.ActuatorManager,
                    MessageType.ActuatorStatus
                );

                await _eventBus.PublishAsync(statusEnvelope);
            }
        }

        public void Dispose()
        {
            // Iterate through the actuators and unsubscribe from their state change events
            foreach (var item in _stateChangedHandlers)
                item.Key.OnStateChanged -= item.Value;
            _stateChangedHandlers.Clear();
        }

        public void Start()
        {
            _eventBus.Subscribe<ActuatorCommandMessage>(HandleActuatorCommand);
        }

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
                MessageOrigin.ActuatorManager,
                MessageType.ActuatorCommandAck
            );

            await _eventBus.PublishAsync(envelope);
        }

        public void Stop()
        {
            _eventBus.Unsubscribe<ActuatorCommandMessage>(HandleActuatorCommand);
        }
    }
}
