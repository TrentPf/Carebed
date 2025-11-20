using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message
{
    public class AlertActionMessage: IEventMessage
    {
       /// <summary>
       /// The sensor or alert source the UI is responding to.
       /// </summary>
       public string Source { get; set; } = string.Empty;

        public bool Acknowledge { get; set; }

        public DateTime CreatedAt { get; } = DateTime.Now;
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public bool IsCritical => false;

        public MessageTypeEnum MessageType => MessageTypeEnum.AlertAction;
        public IReadOnlyDictionary<string,string>? Metadata { get; set; }



    }
}
