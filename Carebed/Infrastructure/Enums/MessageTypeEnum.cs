using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Enums
{
    /// <summary>
    /// Provides a high-level category or tag for the message.
    /// Useful for routing, prioritization, or UI display.
    /// These should map back to specific message classes in the system.
    /// </summary>
    public enum MessageTypeEnum
    {
        Undefined = 0,
        SensorData,
        Alert,
        ActuatorCommand,
        ActuatorStatus,
        System
        // Add more as needed
    }

}
