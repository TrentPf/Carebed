using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Enums
{
    /// <summary>
    /// Indicates which module originated the message.
    /// Extend this enum as new modules are added.
    /// </summary>
    public enum MessageOrigin
    {
        Unknown = 0,
        SensorManager,
        ActuatorManager,
        LoggingManager,
        SystemInitializer,
        DisplayManager,
        AlertManager,
        NetworkManager,
        EventBus
        // Add more as needed
    }

}
