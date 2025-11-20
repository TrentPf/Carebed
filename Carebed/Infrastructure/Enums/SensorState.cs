using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Enums
{
    /// <summary>
    /// Describes the various states a sensor can be in.
    /// </summary>
    public enum SensorState
    {
        Uninitialized,  // Default state before any initialization
        Initialized,    // Sensor has been initialized but not yet active
        Running,        // Sensor is actively collecting data
        Stopped,        // Sensor has been stopped
        Error,          // Sensor is in an error state
        Calibrating,    // Sensor is undergoing calibration
        Disconnected    // Sensor is disconnected or not reachable
    }
}
