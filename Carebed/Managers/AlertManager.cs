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
    /// Manages alerts coming from sensors and other sources
    /// Subscribes to AlertMessages , tracks active alerts , and republishes them if new or changed.
    /// </summary>
    public class AlertManager : IManager
    {
        private readonly IEventBus _eventBus;

        //tracks the current active alerts per sensor
        private readonly Dictionary<string, IEventMessage> _activeSensorAlerts = new();

        // Tracks the current active alerts per actuator
        private readonly Dictionary<string, IEventMessage> _activeActuatorAlerts = new();

        // A general dictionary to hold active alerts from various origins
        private readonly Dictionary<MessageOrigins, Dictionary<string, IEventMessage>> _activeAlerts = new();

        private readonly Action<MessageEnvelope<AlertMessage<SensorStatusMessage>>> _sensorStatusHandler;
        private readonly Action<MessageEnvelope<AlertMessage<SensorErrorMessage>>> _sensorErrorHandler;
        private readonly Action<MessageEnvelope<AlertMessage<SensorTelemetryMessage>>> _sensorTelemetryHandler;

        private readonly Action<MessageEnvelope<AlertMessage<ActuatorStatusMessage>>> _actuatorStatusHandler;
        private readonly Action<MessageEnvelope<AlertMessage<ActuatorErrorMessage>>> _actuatorErrorHandler;
        private readonly Action<MessageEnvelope<AlertMessage<ActuatorTelemetryMessage>>> _actuatorTelemetryHandler;

        private readonly Action<MessageEnvelope<AlertActionMessage<object>>> _uiAlertActionHandler;



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
            _uiAlertActionHandler = HandleAlertActions;

            // Subscribe to sensor alert messages
            _eventBus.Subscribe(_sensorStatusHandler);
            _eventBus.Subscribe(_sensorErrorHandler);
            _eventBus.Subscribe(_sensorTelemetryHandler);

            // Subscribe to actuator alert messages
            _eventBus.Subscribe(_actuatorStatusHandler);
            _eventBus.Subscribe(_actuatorErrorHandler);
            _eventBus.Subscribe(_actuatorTelemetryHandler);

            // Subscribe to alert action messages from the UI
            _eventBus.Subscribe(_uiAlertActionHandler);

        }

        private void HandleUiAlertActions(MessageEnvelope<AlertActionMessage<object>> envelope)
        {
            throw new NotImplementedException();
        }

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


        ///<summary>
        ///Handles Message coming back from the UI(acknowledge)
        ///</summary>
        
        private void HandleAlertAction(MessageEnvelope<AlertActionMessage>envelope)
        {
            var action = envelope.Payload;
            var sensor = action.Source;

            if (action.Acknowledge)
            {
                AcknowledgeAlert(sensor);
            }
        }


        /// <summary>
        /// Mark a specfic sensors alert  as acknowledged
        /// </summary>
        /// <param name="sensor"></param>
        public void AcknowledgeAlert(string sensor)
        { 
            if (_activeSensorAlerts.TryGetValue(sensor, out var alert)&& alert != null)
            {
                // Mark as acknowledged and clear event
                alert.IsAcknowledged = true;
                _activeSensorAlerts[sensor] = null;
            }
            
        }

        public void Start()
        {

        }
        public void Stop()
        {

        }

        public void Dispose()
        {

        }


    }



}