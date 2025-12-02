using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Carebed.Infrastructure;
using Carebed.Infrastructure.EventBus;
using Carebed.Managers;
using Carebed.UI;
using Carebed.Models.Actuators;
using Carebed.Models.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carebed.Tests.Infrastructure
{
    [TestClass]
    public class SystemInitializerTests
    {
        [TestMethod]
        public void Initialize_Returns_Valid_Objects_And_Managers_Are_Started()
        {
            // Act
            var (eventBus, managers, dashboard) = SystemInitializer.Initialize();

            // Assert
            Assert.IsNotNull(eventBus, "EventBus should not be null.");
            Assert.IsInstanceOfType(eventBus, typeof(BasicEventBus));

            Assert.IsNotNull(managers, "Managers list should not be null.");
            Assert.IsTrue(managers.Count >= 4, "Managers list should contain at least 4 managers (Sensor, Actuator, Alert, Logging).");
            Assert.IsTrue(managers.Any(m => m.GetType().Name == "SensorManager"), "Managers should contain SensorManager.");
            Assert.IsTrue(managers.Any(m => m.GetType().Name == "ActuatorManager"), "Managers should contain ActuatorManager.");
            Assert.IsTrue(managers.Any(m => m.GetType().Name == "AlertManager"), "Managers should contain AlertManager.");
            Assert.IsTrue(managers.Any(m => m.GetType().Name == "LoggingManager"), "Managers should contain LoggingManager.");

            Assert.IsNotNull(dashboard, "Dashboard should not be null.");
            Assert.IsInstanceOfType(dashboard, typeof(MainDashboard));
        }

        [TestMethod]
        public void Initialize_SensorManager_And_ActuatorManager_Have_Expected_Devices()
        {
            // Act
            var (_, managers, _) = SystemInitializer.Initialize();

            var sensorManager = managers.Find(m => m.GetType().Name == "SensorManager");
            var actuatorManager = managers.Find(m => m.GetType().Name == "ActuatorManager");

            Assert.IsNotNull(sensorManager, "SensorManager should be present in managers.");
            Assert.IsNotNull(actuatorManager, "ActuatorManager should be present in managers.");

            // Use reflection to access private _sensors field
            var sensorsField = sensorManager.GetType().GetField("_sensors", BindingFlags.NonPublic | BindingFlags.Instance);
            var sensors = sensorsField?.GetValue(sensorManager) as IEnumerable<AbstractSensor>;
            var sensorIds = sensors?.Select(s => s.SensorID).ToList();
            CollectionAssert.IsSubsetOf(new[] { "temp_sensor", "blood_o2_sensor", "eeg_sensor", "heart_rate_sensor" }, sensorIds);

            // Use reflection to access private _actuators field (Dictionary<string, IActuator>)
            var actuatorsField = actuatorManager.GetType().GetField("_actuators", BindingFlags.NonPublic | BindingFlags.Instance);
            var actuatorsDict = actuatorsField?.GetValue(actuatorManager) as IDictionary<string, IActuator>;
            var actuatorNames = actuatorsDict?.Values.Select(a => a.ActuatorId).ToList();
            CollectionAssert.Contains(actuatorNames, "SimulatedBedLampActuator1");
        }
    }
}