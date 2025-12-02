
namespace Carebed.Infrastructure.Message.AlertMessages
{
    /// <summary>
    /// This class represents an alert clear message with a generic payload.
    /// </summary>
    /// <typeparam name="TPayload"></typeparam>
    public class AlertClearMessage<TPayload> : AlertBaseMessage<TPayload> where TPayload : IEventMessage
    {
        public int alertNumber { get; set; }

        public bool clearAllMessages { get; set; }
    }
}
