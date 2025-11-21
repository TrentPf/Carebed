using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    public class SensorCommandMessage: SensorMessageBase
    {
        /// <summary>
        /// The type of command to execute (e.g., Raise, Lower, Stop).
        /// Defined as an enum for clarity and safety.
        /// </summary>
        public required SensorCommands CommandType { get; set; }

        /// <summary>
        /// Optional parameters for the command (e.g., target angle, duration).
        /// Can be empty for simple commands.
        /// </summary>
        public Dictionary<string, object>? Parameters { get; set; } = new();
    }
}
