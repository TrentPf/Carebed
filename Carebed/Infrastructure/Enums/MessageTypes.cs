namespace Carebed.Infrastructure.Enums
{
    /// <summary>
    /// Provides a high-level category or tag for the message.
    /// Useful for routing, prioritization, or UI display.
    /// These should map back to specific message classes in the system.
    /// </summary>
    public enum MessageTypes
    {
        Undefined = 0,
        SensorData,
        Alert,
        AlertClear,
        ActuatorCommand,
        ActuatorCommandAck,
        ActuatorStatus,
        ActuatorError,
        ActuatorTelemetry,
        System,
        AlertAction,
        AlertClearAck,
        ActuatorInventory,
        SensorCommand,
        SensorCommandAck,
        SensorInventory,
        SensorError,
        SensorStatus,
        LoggerCommandResponse
        // Add more as needed
    }

}
