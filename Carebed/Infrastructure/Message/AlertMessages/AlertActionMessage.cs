namespace Carebed.Infrastructure.Message.AlertMessages
{
    public class AlertActionMessage<TPayload>: AlertBaseMessage<TPayload> where TPayload : IEventMessage
    {   
    }
}
