using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Sensors;
using Carebed.Managers;
//using Carebed.Models.Sensors; // for ISensor and concrete sensors
using Carebed.Modules;

namespace Carebed.Infrastructure
{
    internal static class SystemInitializer
    {
        public static (BasicEventBus eventBus, List<IManager> managers) Initialize()
        {
            var eventBus = new BasicEventBus();

            // Create real sensors
            //var sensors = new List<ISensor>
            //{
            //    new TemperatureSensor(),
            //    new PressureSensor(),
            //    // ... add more sensors as needed
            //};

            var sensorManager = new SimulatedSensorManager(eventBus);
            // var actuatorManager = new ActuatorManager(eventBus, actuators);
            // var displayManager = new DisplayManager(eventBus, displays);

            var managers = new List<IManager>
            {
                sensorManager
                // actuatorManager,
                // displayManager
            };

            return (eventBus, managers);
        }
    }
}