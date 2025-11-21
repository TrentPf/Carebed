using Carebed.Infrastructure.Message.Actuator;
using Carebed.Models.Actuators;

namespace Carebed.Infrastructure.Message.ActuatorMessages
{
    public class ActuatorInventoryMessage : ActuatorMessageBase
    {
        public List<IActuator> Actuators { get; set; } = new();
    }
}
