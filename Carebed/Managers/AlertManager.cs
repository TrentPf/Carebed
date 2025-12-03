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
    public class AlertManager : IManager
    {
        #region Fields

        private readonly IEventBus _eventBus;

        // Alert sequence counter
        private int _alertSequence = 0;

        //tracks the current active alerts per sensor
        private readonly Dictionary<string, IEventMessage> _activeSensorAlerts = new();

        // Tracks the current active alerts per actuator
        private readonly Dictionary<string, IEventMessage> _activeActuatorAlerts = new();

        // A general dictionary to hold active alerts from various origins
        private readonly Dictionary<MessageOrigins, Dictionary<string, IEventMessage>> _activeAlerts = new();

        // concrete handlers for sensor messages
        private readonly Action<MessageEnvelope<SensorTelemetryMessage>> _sensorTelemetryHandler;
        private readonly Action<MessageEnvelope<SensorStatusMessage>> _sensorStatusHandler;
        private readonly Action<MessageEnvelope<SensorErrorMessage>> _sensorErrorHandler;

        // concrete handlers for actuator messages
        private readonly Action<MessageEnvelope<ActuatorTelemetryMessage>> _actuatorTelemetryHandler;
        private readonly Action<MessageEnvelope<ActuatorStatusMessage>> _actuatorStatusHandler;
        private readonly Action<MessageEnvelope<ActuatorErrorMessage>> _actuatorErrorHandler;

        // Handler for alert clear messages from the UI
        private readonly Action<MessageEnvelope<AlertClearMessage<IEventMessage>>> _uiAlertClearActionHandler;
        #endregion

        #region Constructor(s)

        public AlertManager(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            // initialize dictionaries for expected origins
            _activeAlerts[MessageOrigins.SensorManager] = _activeSensorAlerts;
            _activeAlerts[MessageOrigins.ActuatorManager] = _activeActuatorAlerts;

            // setup handlers
            _sensorTelemetryHandler = env => HandleSensorMessage(env);
            _sensorStatusHandler = env => HandleSensorMessage(env);
            _sensorErrorHandler = env => HandleSensorMessage(env);

            _actuatorTelemetryHandler = env => HandleActuatorMessage(env);
            _actuatorStatusHandler = env => HandleActuatorMessage(env);
            _actuatorErrorHandler = env => HandleActuatorMessage(env);

            _uiAlertClearActionHandler = HandleAlertClear;
        }

        #endregion

        #region Event Handlers

        private void HandleSensorMessage<T>(MessageEnvelope<T> envelope) where T : SensorMessageBase
        {
            var payload = envelope.Payload;
            if (payload == null) return;

            bool isAlert = false;

            // Determine alert conditions
            if (payload is SensorErrorMessage)
            {
                isAlert = true;
            }
            else if (payload is SensorStatusMessage statusMsg)
            {
                if (statusMsg.CurrentState == SensorStates.Error)
                    isAlert = true;
            }
            else if (payload is SensorTelemetryMessage telemetry)
            {
                if (telemetry.Data != null && telemetry.Data.IsCritical)
                    isAlert = true;
            }

            if (!isAlert) return;

            var sensorId = payload.SensorID ?? "Unknown";
            var origin = envelope.MessageOrigin == 0 ? MessageOrigins.SensorManager : envelope.MessageOrigin;

            var activeDict = _activeAlerts.ContainsKey(origin) ? _activeAlerts[origin] : null;
            if (activeDict != null)
            {
                if (activeDict.TryGetValue(sensorId, out var existing) && existing != null && existing.CorrelationId == payload.CorrelationId)
                {
                    // duplicate alert
                    return;
                }
                activeDict[sensorId] = payload;
            }

            // create and publish AlertActionMessage<T>
            var alertAction = new AlertActionMessage<T>
            {
                Source = sensorId,
                AlertText = payload is SensorErrorMessage err ? err.Description : (payload is SensorTelemetryMessage tel ? $"{tel.Data?.Value:F2}" : "Sensor alert"),
                Payload = payload,
                alertNumber = ++_alertSequence
            };

            var alertEnvelope = new MessageEnvelope<AlertActionMessage<T>>(alertAction, MessageOrigins.AlertManager, MessageTypes.AlertAction);
            _ = _eventBus.PublishAsync(alertEnvelope);
        }

        private void HandleActuatorMessage<T>(MessageEnvelope<T> envelope) where T : ActuatorMessageBase
        {
            var payload = envelope.Payload;
            if (payload == null) return;

            bool isAlert = false;

            if (payload is ActuatorErrorMessage)
                isAlert = true;
            else if (payload is ActuatorStatusMessage status && status.CurrentState == ActuatorStates.Error)
                isAlert = true;
            else if (payload is ActuatorTelemetryMessage telemetry && payload.IsCritical)
                isAlert = true;

            if (!isAlert) return;

            var actuatorId = payload.ActuatorId ?? "Unknown";
            var origin = envelope.MessageOrigin == 0 ? MessageOrigins.ActuatorManager : envelope.MessageOrigin;

            var activeDict = _activeAlerts.ContainsKey(origin) ? _activeAlerts[origin] : null;
            if (activeDict != null)
            {
                if (activeDict.TryGetValue(actuatorId, out var existing) && existing != null && existing.CorrelationId == payload.CorrelationId)
                {
                    return;
                }
                activeDict[actuatorId] = payload;
            }

            var alertAction = new AlertActionMessage<T>
            {
                Source = actuatorId,
                AlertText = payload is ActuatorErrorMessage err ? err.Description : "Actuator alert",
                Payload = payload,
                alertNumber = ++_alertSequence
            };

            var alertEnvelope = new MessageEnvelope<AlertActionMessage<T>>(alertAction, MessageOrigins.AlertManager, MessageTypes.AlertAction);
            _ = _eventBus.PublishAsync(alertEnvelope);
        }

        private void HandleAlertClear(MessageEnvelope<AlertClearMessage<IEventMessage>> envelope)
        {
            bool alertNotFound = false;

            var message = envelope.Payload;
            if (message == null ) return;

            if (message.clearAllMessages)
            {
                // Clear all alerts logic here
                _activeAlerts.Clear();

                // Optionally, send an ack message
                var ack = new AlertClearAckMessage { Source = "ALL", alertCleared = true };
                var ackEnv = new MessageEnvelope<AlertClearAckMessage>(ack, MessageOrigins.AlertManager, MessageTypes.AlertClearAck);
                _ = _eventBus.PublishAsync(ackEnv);
                return;
            }

            if (message.Payload == null) return;

            if (message.Payload is SensorMessageBase sensorMsg)
            {
                var sensorId = sensorMsg.SensorID;
                if (_activeAlerts.TryGetValue(MessageOrigins.SensorManager, out var dict) && dict.TryGetValue(sensorId, out var _))
                {
                    dict.Remove(sensorId);
                    var ack = new AlertClearAckMessage { Source = sensorId, CorrelationId = message.Payload.CorrelationId, alertNumber = message.alertNumber };
                    var ackEnv = new MessageEnvelope<AlertClearAckMessage>(ack, MessageOrigins.AlertManager, MessageTypes.AlertClearAck);
                    _ = _eventBus.PublishAsync(ackEnv);
                }
                else
                {
                    alertNotFound = true;
                }
            }
            else if (message.Payload is ActuatorMessageBase actuatorMsg)
            {
                var actId = actuatorMsg.ActuatorId;
                if (_activeAlerts.TryGetValue(MessageOrigins.ActuatorManager, out var dict) && dict.TryGetValue(actId, out var _))
                {
                    dict.Remove(actId);
                    var ack = new AlertClearAckMessage { Source = actId, CorrelationId = message.Payload.CorrelationId, alertNumber = message.alertNumber };
                    var ackEnv = new MessageEnvelope<AlertClearAckMessage>(ack, MessageOrigins.AlertManager, MessageTypes.AlertClearAck);
                    _ = _eventBus.PublishAsync(ackEnv);
                }
                else
                {
                    alertNotFound = true;
                }
            }

            if (alertNotFound)
            {
                var notFoundAck = new AlertClearAckMessage { Source = message.Source, CorrelationId = message.Payload.CorrelationId, alertCleared = false, alertNumber = message.alertNumber };
                var notFoundEnv = new MessageEnvelope<AlertClearAckMessage>(notFoundAck, MessageOrigins.AlertManager, MessageTypes.AlertClearAck);
                _ = _eventBus.PublishAsync(notFoundEnv);
            }
        }

        #endregion

        #region Methods

        public void Start()
        {
            _eventBus.Subscribe(_sensorTelemetryHandler);
            _eventBus.Subscribe(_sensorStatusHandler);
            _eventBus.Subscribe(_sensorErrorHandler);

            _eventBus.Subscribe(_actuatorTelemetryHandler);
            _eventBus.Subscribe(_actuatorStatusHandler);
            _eventBus.Subscribe(_actuatorErrorHandler);

            _eventBus.Subscribe(_uiAlertClearActionHandler);
        }

        public void Stop()
        {
            _eventBus.Unsubscribe(_sensorTelemetryHandler);
            _eventBus.Unsubscribe(_sensorStatusHandler);
            _eventBus.Unsubscribe(_sensorErrorHandler);

            _eventBus.Unsubscribe(_actuatorTelemetryHandler);
            _eventBus.Unsubscribe(_actuatorStatusHandler);
            _eventBus.Unsubscribe(_actuatorErrorHandler);

            _eventBus.Unsubscribe(_uiAlertClearActionHandler);
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion
    }
}