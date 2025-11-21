using Carebed.Infrastructure.EventBus;
using Carebed.Managers;
using Carebed.Models.Actuators;
using Carebed.Models.Sensors;

namespace Carebed.Infrastructure
{
    public static class SystemInitializer
    {
        public static (BasicEventBus eventBus, List<IManager> managers) Initialize()
        {
            var _eventBus = new BasicEventBus();
            
            // Create a list of simulated sensors
            var sensors = new List<ISensor>
            {
                new TemperatureSensor("temp_sensor"),
                new BloodOxygenSensor("blood_o2_sensor"),
                new EegSensor("eeg_sensor"),
                new HeartRateSensor("heart_rate_sensor")
                // ... add more sensors as needed
            };

            var actuators = new List<IActuator>
            {
                new SimulatedBedLamp("SimulatedBedLampActuator1")
                // ... add more actuators as needed
            };

            var sensorManager = new SensorManager(_eventBus, sensors);
            var actuatorManager = new ActuatorManager(_eventBus, actuators);

            var managers = new List<IManager>
            {
                sensorManager,
                actuatorManager
            };

            // Start the managers
            foreach (var manager in managers)
            {
                manager.Start();
            }

            return (_eventBus, managers);
        }
    }
}