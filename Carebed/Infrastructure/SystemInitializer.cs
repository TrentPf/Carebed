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
            var eventBus = new BasicEventBus();
            
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

            var sensorManager = new SensorManager(eventBus, sensors);
            var actuatorManager = new ActuatorManager(eventBus, actuators);

            var managers = new List<IManager>
            {
                sensorManager,
                actuatorManager
            };

            return (eventBus, managers);
        }

        public static void StartManagers(List<IManager> managers)
        {
            foreach (var manager in managers)
            {
                manager.Start();
            }
        }

        public static void StopManagers(List<IManager> managers)
        {
            foreach (var manager in managers)
            {
                manager.Stop();
            }
        }

        public static void DisposeManagers(List<IManager> managers)
        {
            foreach (var manager in managers)
            {
                manager.Dispose();
            }
        }

        public static void EmitDeviceList(BasicEventBus eventBus, List<ISensor> sensors, List<IActuator> actuators)
        {
            // Create a 
            eventBus.Publish("DeviceListUpdated", deviceList);
        }


    }
}