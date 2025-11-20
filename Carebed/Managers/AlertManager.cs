using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
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
        private readonly Dictionary<string, AlertMessage?> _activeAlerts = new();

        public AlertManager(IEventBus eventBus)
        {
            _eventBus = eventBus;
            //subscribe to alerts published on eventbus
            _eventBus.Subscribe<AlertMessage>(HandleSensorAlert);
            _eventBus.Subscribe<AlertActionMessage>(HandleAlertAction);

        }

        /// <summary>
        /// Handles incoming alert messages from sensors or other sources
        /// </summary>
        /// <param name="envelope"></param>
       
        private void HandleSensorAlert(MessageEnvelope<AlertMessage> envelope)
        {
            var data = envelope.Payload;
            var source = data.Source ?? "<Unknown>";
            var alertText = data.AlertText;
            var isCritical = data.IsCritical;

            AlertMessage? previousAlert = null;
            _activeAlerts.TryGetValue(source, out previousAlert);

            //only store and republish if this is a new alert or changed alert
            if (previousAlert == null || previousAlert.AlertText != alertText)
            {
                var alert = new AlertMessage
                {
                    Source = source,
                    AlertText = alertText,
                    IsCritical = isCritical,
                    IsAcknowledged = false
                };
                // store active alert
                _activeAlerts[source] = alert;

                //publish the alert to the bus for UI or other subsribers
                _ = _eventBus.PublishAsync(new MessageEnvelope<AlertMessage>(alert, MessageOriginEnum.AlertManager, MessageTypeEnum.Alert));
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
            if (_activeAlerts.TryGetValue(sensor, out var alert)&& alert != null)
            {
                // Mark as acknowledged and clear event
                alert.IsAcknowledged = true;
                _activeAlerts[sensor] = null;
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