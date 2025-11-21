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
        ActuatorCommand,
        ActuatorCommandAck,
        ActuatorStatus,
        ActuatorError,
        ActuatorTelemetry,
        System,
        AlertAction,
        AlertClearAck
        // Add more as needed
    }

}
