using System;
using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Logging
{
    internal class LogMessage
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public LogLevelEnum Level { get; set; } = LogLevelEnum.Info;
        public MessageOriginEnum Origin { get; set; } = MessageOriginEnum.Unknown;
        public MessageTypeEnum Type { get; set; } = MessageTypeEnum.Undefined;
        public string Message { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
    }
}