using Carebed.Infrastructure.EventBus;
using Carebed.Managers;
using Carebed.UI;
using Carebed.Models.Actuators;
using Carebed.Models.Sensors;
using Carebed.Infrastructure.Logging;

namespace Carebed.Infrastructure
{
    public static class SystemInitializer
    {
        public static (BasicEventBus eventBus, List<IManager> managers, MainDashboard dashboard) Initialize()
        {
            var _eventBus = new BasicEventBus();
            
            // Create a list of simulated sensors
            var sensors = new List<AbstractSensor>
            {
                new TemperatureSensor("temp_sensor"),
                new BloodOxygenSensor("blood_o2_sensor"),
                new EegSensor("eeg_sensor"),
                new HeartRateSensor("heart_rate_sensor"),
                new PaitentUpSensor("Paitent_up_sensor")
                // ... add more sensors as needed
            };

            var actuators = new List<IActuator>
            {
                new SimulatedBedLamp("SimulatedBedLampActuator1")
                // ... add more actuators as needed
            };


            // Create SensorManager
            var sensorManager = new SensorManager(_eventBus, sensors, 15000);

            // Create ActuatorManager
            var actuatorManager = new ActuatorManager(_eventBus, actuators);

            // Create AlertManager
            var alertManager = new AlertManager(_eventBus);

            // Create LoggingManager
            string logDir = "Logs"; // Define log directory
            string logFileName = "app_log.txt"; // Define log file name
            string combinedFilePath = Path.Combine(logDir, logFileName); // Combine directory and file name
            IFileLoggingService fileLogger = new SimpleFileLogger(combinedFilePath);
            var loggingManager = new LoggingManager(logDir, logFileName, fileLogger, _eventBus);

            var managers = new List<IManager>
            {
                sensorManager,
                actuatorManager,
                alertManager,
                loggingManager
            };

            // Instantiate the MainDashboard, pass sensorManager and alertManager so UI controls their lifecycles
            var dashboard = new MainDashboard(_eventBus);

            // Start managers
            foreach (var manager in managers)
            {
                manager.Start();
            }

            return (_eventBus, managers, dashboard);
        }
    }
}