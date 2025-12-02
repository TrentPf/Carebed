using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    public class SensorCommandAckMessage: SensorMessageBase
    {
        /// <summary>
        /// Represents the type of command that was acknowledged.
        /// </summary>
        public required SensorCommands CommandType { get; set; }

        /// <summary>
        /// Represents whether the Sensor executed the given command successfully.
        /// </summary>
        public required bool CommandExecutedSuccessfully { get; set; }

        /// <summary>
        /// Represents an optional reason for the command acknowledgment. 
        /// Usually used with failed acknowledgments to provide context.
        /// </summary>
        public string? Reason { get; set; } = null;
    }
}
