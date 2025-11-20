using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;


namespace Carebed.Infrastructure.Message
{
    public class AlertMessage : IEventMessage
    {
        public DateTime CreatedAt { get; } = DateTime.Now;
        public Guid CorrelationId { get; } = Guid.NewGuid();

        public string? Source { get; set; }
        public string AlertText { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
        public bool IsWarning { get; set; } = false;
        public bool IsAcknowledged { get; set; } = false;

        public MessageTypeEnum MessageType { get; } = MessageTypeEnum.Alert;
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }


    }



}
        
