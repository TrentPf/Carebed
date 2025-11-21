namespace Carebed.Infrastructure.Message.AlertMessages
{
    public class AlertMessage<TPayload> : AlertBaseMessage<TPayload> where TPayload : IEventMessage
    {
    }
}
        
