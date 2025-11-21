using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.Actuator;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.Message.AlertMessages;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.MessageEnvelope;

namespace Carebed.Managers
{
    /// <summary>
    /// Manager responsible for handling alerts from sensors and actuators, tracking active alerts, and sending alert messages to the UI layer.
    /// </summary>
    public class AlertManager : IManager
    {
        #region Fields

        private readonly IEventBus _eventBus;

        //tracks the current active alerts per sensor
        private readonly Dictionary<string, IEventMessage> _activeSensorAlerts = new();

        // Tracks the current active alerts per actuator
        private readonly Dictionary<string, IEventMessage> _activeActuatorAlerts = new();

        // A general dictionary to hold active alerts from various origins
        private readonly Dictionary<MessageOrigins, Dictionary<string, IEventMessage>> _activeAlerts = new();

        // Handlers for sensor alert messages
        private readonly Action<MessageEnvelope<AlertMessage<SensorStatusMessage>>> _sensorStatusHandler;
        private readonly Action<MessageEnvelope<AlertMessage<SensorErrorMessage>>> _sensorErrorHandler;
        private readonly Action<MessageEnvelope<AlertMessage<SensorTelemetryMessage>>> _sensorTelemetryHandler;

        // Handlers for actuator alert messages
        private readonly Action<MessageEnvelope<AlertMessage<ActuatorStatusMessage>>> _actuatorStatusHandler;
        private readonly Action<MessageEnvelope<AlertMessage<ActuatorErrorMessage>>> _actuatorErrorHandler;
        private readonly Action<MessageEnvelope<AlertMessage<ActuatorTelemetryMessage>>> _actuatorTelemetryHandler;

        // Handler for alert clear messages from the UI
        private readonly Action<MessageEnvelope<AlertClearMessage<IEventMessage>>> _uiAlertClearActionHandler;
        #endregion

        #region Constructor(s)

        public AlertManager(IEventBus eventBus)
        {
            _eventBus = eventBus;

            // Assign the sensor handlers
            _sensorStatusHandler = HandleSensorAlerts;
            _sensorErrorHandler = HandleSensorAlerts;
            _sensorTelemetryHandler = HandleSensorAlerts;

            // Assign the actuator handlers
            _actuatorStatusHandler = HandleActuatorAlerts;
            _actuatorErrorHandler = HandleActuatorAlerts;
            _actuatorTelemetryHandler = HandleActuatorAlerts;

            // Assign the alert action handler
            _uiAlertClearActionHandler = HandleAlertClear;           

        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles alert clear messages from the UI. Clears alert from local registry, then publishes confirmation messages back to the eventBus for the UI to pick up.
        /// <br/> If no matching alert is found, publishes a negative acknowledgment indicating that it was unable to find the alert.
        /// </summary>
        /// <param name="envelope"></param>
        private void HandleAlertClear(MessageEnvelope<AlertClearMessage<IEventMessage>> envelope)
        {
            bool alertNotFound = false;

            // Create a variable to hold the incoming alert clear message
            var message = envelope.Payload;

            // Check if the payload is valid
            if (message != null && message.Payload != null)
            {
                // Cast to IEventMessage to access common properties
                if (message is IEventMessage eventMsg)
                {                    
                    // For sensors:
                    if (eventMsg is SensorMessageBase sensorMsg)
                    {
                        // Convenience variable
                        var sensorId = sensorMsg.SensorID;

                        // Determine if this alert exists in the active alerts registry
                        if(_activeAlerts[MessageOrigins.SensorManager].TryGetValue(sensorId, out var existingAlert))
                        {
                            // Alert exists - Remove it from the active alerts registry
                            _activeAlerts[MessageOrigins.SensorManager].Remove(sensorId);

                            // Create a new AlertClearAckMessage to acknowledge the alert clearance
                            // Be sure to set the CorrelationId to match the original alert
                            var newAlertClearedAckMessage = new AlertClearAckMessage
                            {
                                Source = sensorId,
                                CorrelationId = eventMsg.CorrelationId
                            };

                            //create a new envelope for the alert clear message
                            var alertClearEnvelope = new MessageEnvelope<AlertClearAckMessage>(
                                newAlertClearedAckMessage,
                                MessageOrigins.AlertManager,
                                MessageTypes.AlertClearAck);

                            //publish the alert clear message to the event bus
                            _eventBus.PublishAsync(alertClearEnvelope);
                        }
                        else
                        {
                            alertNotFound = true;                            
                        }
                    }
                    // For actuators:
                    else if (eventMsg is ActuatorMessageBase actuatorMsg)
                    {
                        var actuatorId = actuatorMsg.ActuatorId;

                        // Determine if this alert exists in the active alerts registry
                        if (_activeAlerts[MessageOrigins.SensorManager].TryGetValue(actuatorId, out var existingAlert))
                        {
                            // Alert exists - Remove it from the active alerts registry
                            _activeAlerts[MessageOrigins.SensorManager].Remove(actuatorId);

                            // Send acknowledgment for the UI
                            var newAlertClearedAckMessage = new AlertClearAckMessage
                            {
                                Source = actuatorId,
                                CorrelationId = eventMsg.CorrelationId
                            };

                            //create a new envelope for the alert clear message
                            var alertClearEnvelope = new MessageEnvelope<AlertClearAckMessage>(
                                newAlertClearedAckMessage,
                                MessageOrigins.AlertManager,
                                MessageTypes.AlertClearAck);

                            //publish the alert clear message to the event bus
                            _eventBus.PublishAsync(alertClearEnvelope);
                        }
                    }

                    if (alertNotFound)
                    {
                        // Generate an ack with a negative response to indicate that no such alert was found
                        var alertNotFoundAckMessage = new AlertClearAckMessage
                        {
                            Source = message.Source,
                            CorrelationId = eventMsg.CorrelationId,
                            alertCleared = false
                        };

                        //create a new envelope for the alert clear message
                        var alertNotFoundEnvelope = new MessageEnvelope<AlertClearAckMessage>(
                            alertNotFoundAckMessage,
                            MessageOrigins.AlertManager,
                            MessageTypes.AlertClearAck);

                        //publish the alert clear message to the event bus
                        _eventBus.PublishAsync(alertNotFoundEnvelope);
                    }
                }
            }
            else
            {
                //invalid payload
                return;
            }
        }

        /// <summary>
        /// Handles incoming actuator alert messages, checks if they are new or updated, and republishes them as AlertActionMessages if they are. <br/>
        /// Tracks active alerts to avoid duplicate notifications. Duplicate alerts with the same CorrelationId are ignored.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="envelope"></param>
        private void HandleActuatorAlerts<T>(MessageEnvelope<AlertMessage<T>> envelope) where T : ActuatorMessageBase
        {
            // Create a variable to hold the incoming alert message
            var message = envelope.Payload;
            
            // Check if the payload is valid
            if (message != null && message.Payload != null)
            {
                // If the payload is good, check if this is a new alert
                if (!_activeAlerts[MessageOrigins.SensorManager].TryGetValue(message.Payload.ActuatorId, out var existingAlert))
                {
                    // No existing alert for this sensor - Add this one to the registry

                    // Add item to the active alerts dictionary
                    _activeAlerts[MessageOrigins.SensorManager].Add(message.Payload.ActuatorId, message.Payload);
                }
                else
                {
                    // An existing alert is found for this sensor

                    // Compare the correlationID of the existing alert with the new alert
                    if (existingAlert != null && existingAlert.CorrelationId == message.Payload.CorrelationId)
                    {
                        // Alert already exists with the same correlation ID, no need to republish
                        return;
                    }
                }

                // Alert is new - Create a new AlertActionMessage to republish the alert so the UI layer can pick it up


                var newAlertActionMessage = new AlertActionMessage<T>
                {
                    Source = message.Payload.ActuatorId,
                    AlertText = message.AlertText,
                    Payload = message.Payload
                };

                //create a new envelope for the alert message
                var alertEnvelope = new MessageEnvelope<AlertActionMessage<T>>(
                    newAlertActionMessage,
                    envelope.MessageOrigin,
                    envelope.MessageType);

                //publish the alert action message to the event bus
                _eventBus.PublishAsync(alertEnvelope);
            }
            else
            {
                //invalid payload
                return;
            }

        }

        /// <summary>
        /// Handles incoming sensor alert messages, checks if they are new or updated, and republishes them as AlertActionMessages if they are. <br/>
        /// Tracks active alerts to avoid duplicate notifications. Duplicate alerts with the same CorrelationId are ignored.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="envelope"></param>
        private void HandleSensorAlerts<T>(MessageEnvelope<AlertMessage<T>> envelope) where T:SensorMessageBase
        {
            // Create a variable to hold the incoming alert message
            var message = envelope.Payload;

            // Check if the payload is valid
            if (message != null && message.Payload != null)
            {
                // If the payload is good, check if this is a new alert
                if(!_activeAlerts[MessageOrigins.SensorManager].TryGetValue(message.Payload.SensorID, out var existingAlert))
                {
                    // No existing alert for this sensor - Add this one to the registry

                    // Add item to the active alerts dictionary
                    _activeAlerts[MessageOrigins.SensorManager].Add(message.Payload.SensorID, message.Payload);
                }
                else
                {
                    // An existing alert is found for this sensor

                    // Compare the correlationID of the existing alert with the new alert
                    if (existingAlert != null && existingAlert.CorrelationId == message.Payload.CorrelationId)
                    {
                        // Alert already exists with the same correlation ID, no need to republish
                        return;
                    }
                }

                // Alert is new - Create a new AlertActionMessage to republish the alert so the UI layer can pick it up


                var newAlertActionMessage = new AlertActionMessage<T>
                {
                    Source = message.Payload.SensorID,
                    AlertText = message.AlertText,
                    Payload = message.Payload
                };

                //create a new envelope for the alert message
                var alertEnvelope = new MessageEnvelope<AlertActionMessage<T>>(
                    newAlertActionMessage,
                    envelope.MessageOrigin,
                    envelope.MessageType);

                //publish the alert action message to the event bus
                _eventBus.PublishAsync(alertEnvelope);
            }
            else
            {
                //invalid payload
                return;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Starts the AlertManager by subscribing to relevant alert messages from sensors, actuators, and UI actions.
        /// </summary>
        public void Start()
        {
            // Subscribe to sensor alert messages
            _eventBus.Subscribe(_sensorStatusHandler);
            _eventBus.Subscribe(_sensorErrorHandler);
            _eventBus.Subscribe(_sensorTelemetryHandler);

            // Subscribe to actuator alert messages
            _eventBus.Subscribe(_actuatorStatusHandler);
            _eventBus.Subscribe(_actuatorErrorHandler);
            _eventBus.Subscribe(_actuatorTelemetryHandler);

            // Subscribe to alert action messages from the UI
            _eventBus.Subscribe(_uiAlertClearActionHandler);
        }

        /// <summary>
        /// Stops the AlertManager by unsubscribing from all alert messages.
        /// </summary>
        public void Stop()
        {
            // Subscribe to sensor alert messages
            _eventBus.Unsubscribe(_sensorStatusHandler);
            _eventBus.Unsubscribe(_sensorErrorHandler);
            _eventBus.Unsubscribe(_sensorTelemetryHandler);

            // Subscribe to actuator alert messages
            _eventBus.Unsubscribe(_actuatorStatusHandler);
            _eventBus.Unsubscribe(_actuatorErrorHandler);
            _eventBus.Unsubscribe(_actuatorTelemetryHandler);

            // Subscribe to alert action messages from the UI
            _eventBus.Unsubscribe(_uiAlertClearActionHandler);
        }

        /// <summary>
        /// Stops the AlertManager to release any resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        #endregion
    }
}