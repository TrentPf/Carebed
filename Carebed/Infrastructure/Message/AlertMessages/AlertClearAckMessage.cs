namespace Carebed.Infrastructure.Message.AlertMessages
{
    public class AlertClearAckMessage : AlertBaseMessage<object?>
    {
        public AlertClearAckMessage()
        {
            Payload = null;
        }

        public bool alertCleared = true;
    }
}
