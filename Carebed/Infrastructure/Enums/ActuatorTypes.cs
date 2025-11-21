using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Enums
{
    /// <summary>
    /// A list of actuator types used in the care bed system.
    /// </summary>
    public enum ActuatorTypes
    {
        BedLift,       // Controls vertical elevation of the bed frame
        HeadTilt,      // Adjusts the angle of the head section
        LegRaise,      // Raises or lowers the leg section
        SideRail,      // Controls deployment of safety rails
        BedRoll,       // Enables lateral rolling or repositioning
        Lamp,          // Controls the bed lamp (on/off)
        Custom,         // Reserved for future or specialized actuators
        Manager
    }

}
