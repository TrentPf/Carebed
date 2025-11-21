using Carebed.Models.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Infrastructure.Message.SensorMessages
{
    public class SensorInventoryMessage : SensorMessageBase
    {
        public List<ISensor> Sensors { get; set; } = new();
    }
}
