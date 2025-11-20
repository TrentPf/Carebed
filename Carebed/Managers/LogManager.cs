
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Logging;

namespace Carebed.Managers
{
    internal sealed class LoggingManager : IDisposable
    {
        private readonly ILoggingService _logger;
        private static readonly Lazy<LoggingManager> _instance = new(() => new LoggingManager());
        public static LoggingManager Instance => _instance.Value;

        private LoggingManager()
        {
            var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var filePath = System.IO.Path.Combine(logDir, "carebed.log");
            _logger = new SimpleFileLogger(filePath);
            // start immediately for MVP
            _logger.StartAsync().GetAwaiter().GetResult();
        }

        public void LogSensorData<T>(T payload, string message = "SensorData", LogLevelEnum level = LogLevelEnum.Info)
        {
            var lm = new LogMessage
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Origin = MessageOrigin.SensorManager,
                Type = MessageType.SensorData,
                Message = message,
                PayloadJson = SerializePayload(payload)
            };
            _logger.Log(lm);
        }

        public void Log(MessageOrigin origin, MessageType type, string message, object? payload = null, LogLevelEnum level = LogLevelEnum.Info)
        {
            var lm = new LogMessage
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Origin = origin,
                Type = type,
                Message = message,
                PayloadJson = SerializePayload(payload)
            };
            _logger.Log(lm);
        }

        private static string SerializePayload(object? payload)
        {
            if (payload == null) return string.Empty;
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return payload.ToString() ?? string.Empty;
            }
        }

        public Task ShutdownAsync()
        {
            return _logger.StopAsync();
        }

        public void Dispose()
        {
            ShutdownAsync().GetAwaiter().GetResult();
            if (_logger is IDisposable d) d.Dispose();
        }
    }
}