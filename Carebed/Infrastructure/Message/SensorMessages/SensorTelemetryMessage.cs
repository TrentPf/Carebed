namespace Carebed.Infrastructure.Message.SensorMessages
{
    public class SensorTelemetryMessage: SensorMessageBase
    {
        public required SensorData Data { get; set; }
    }
}
