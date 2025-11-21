using Carebed.Infrastructure.Enums;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    public class SensorErrorMessage: SensorMessageBase
    {
        /// <summary>
        /// An error code representing the fault condition.
        /// Used for diagnostics, filtering, and automated responses.
        /// </summary>
        public required SensorErrorCodes ErrorCode { get; set; }

        /// <summary>
        /// A human-readable description of the error or fault condition.
        /// Intended for logs, UI alerts, and operator guidance.
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// The current or relevant state of the actuator.
        /// </summary>
        public required SensorStates CurrentState { get; set; }
    }
}
