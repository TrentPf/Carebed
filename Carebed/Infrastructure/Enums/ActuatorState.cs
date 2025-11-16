
namespace Carebed.Infrastructure.Enums
{
    public enum ActuatorState
    {
        Idle,           // The actuator is not currently moving or engaged
        Moving,         // The actuator is in the process of moving
        Completed,      // The actuator has completed its movement
        Error,          // The actuator has encountered an error condition
        Locked,         // The actuator is locked and cannot move
        Initializing    // The actuator is in an initializing state and will be ready soon
    }
}
