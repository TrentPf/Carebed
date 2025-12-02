using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Managers;
using Carebed.Models.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Carebed.Tests.Managers
{
    [TestClass]
    public class SensorManagerTests
    {
        private Mock<IEventBus> _eventBusMock;
        private List<AbstractSensor> _sensors;
        private List<object> _publishedMessages;
        private SensorManager _sensorManager;

        public class TestSensor : AbstractSensor
        {
            public bool Started { get; set; } // Changed from private set to set for test access
            public bool Stopped { get; set; } // Changed from private set to set for test access
            public int ReadCount { get; private set; }
            public SensorStates? LastStateChanged { get; private set; }

            public TestSensor(string id, SensorTypes type) : base(id, type, 0, 100, 90) { }

            public override SensorData ReadDataActual()
            {
                ReadCount++;
                return new SensorData { Value = 42, Source = SensorID, SensorType = SensorType, IsCritical = false };
            }

            public override void Start()
            {
                Started = true;
                base.Start();
            }

            public override void Stop()
            {
                Stopped = true;
                base.Stop();
            }
        }

        [TestInitialize]
        public void Setup()
        {
            _eventBusMock = new Mock<IEventBus>();
            _publishedMessages = new List<object>();
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<IEventMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<SensorTelemetryMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<SensorErrorMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<SensorStatusMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<SensorCommandAckMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<SensorInventoryMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);

            _sensors = new List<AbstractSensor>
            {
                new TestSensor("sensor1", SensorTypes.HeartRate),
                new TestSensor("sensor2", SensorTypes.Temperature)
            };

            _sensorManager = new SensorManager(_eventBusMock.Object, _sensors, 100);
        }

        [TestMethod]
        public void Constructor_ShouldSubscribeAndEmitInventory()
        {
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<SensorCommandMessage>>>()), Times.Once);
            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorInventoryMessage>));
        }

        [TestMethod]
        public void Start_ShouldStartAllSensorsAndTimer()
        {
            _sensorManager.Start();
            foreach (var sensor in _sensors.Cast<TestSensor>())
            {
                Assert.IsTrue(sensor.Started);
            }
        }

        [TestMethod]
        public void Stop_ShouldStopAllSensorsAndTimer()
        {
            _sensorManager.Stop();
            foreach (var sensor in _sensors.Cast<TestSensor>())
            {
                Assert.IsTrue(sensor.Stopped);
            }
        }

        [TestMethod]
        public void StartSensor_ShouldStartSpecificSensor()
        {
            var testSensor = (TestSensor)_sensors[0];
            testSensor.Started = false;
            _sensorManager.StartSensor(testSensor.SensorID);
            Assert.IsTrue(testSensor.Started);
        }

        [TestMethod]
        public void StopSensor_ShouldStopSpecificSensor()
        {
            var testSensor = (TestSensor)_sensors[0];
            testSensor.Stopped = false;
            _sensorManager.StopSensor(testSensor.SensorID);
            Assert.IsTrue(testSensor.Stopped);
        }

        [TestMethod]
        public void AdjustPollingRate_ShouldUpdateInterval()
        {
            Assert.IsTrue(_sensorManager.AdjustPollingRate(2.5));
            Assert.IsFalse(_sensorManager.AdjustPollingRate(0));
            Assert.IsFalse(_sensorManager.AdjustPollingRate(100));
        }

        [TestMethod]
        public async Task PollOnceAsync_ShouldPublishTelemetry()
        {
            _sensorManager.Start(); // Ensure sensors are running

            var pollOnceAsyncMethod = typeof(SensorManager)
                .GetMethod("PollOnceAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = pollOnceAsyncMethod?.Invoke(_sensorManager, null) as Task;
            Assert.IsNotNull(task, "PollOnceAsync method invocation returned null.");
            await task;

            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorTelemetryMessage>));

            _sensorManager.Stop();
        }

        [TestMethod]
        public void HandleSensorCommand_StartStop_AdjustPollingRate()
        {
            var startMsg = new SensorCommandMessage
            {
                CommandType = SensorCommands.StartSensor,
                SensorID = "sensor1",
                TypeOfSensor = SensorTypes.HeartRate
            };
            var stopMsg = new SensorCommandMessage
            {
                CommandType = SensorCommands.StopSensor,
                SensorID = "sensor2",
                TypeOfSensor = SensorTypes.Temperature
            };
            var adjustMsg = new SensorCommandMessage
            {
                CommandType = SensorCommands.AdjustPollingRate,
                SensorID = "sensor1",
                TypeOfSensor = SensorTypes.HeartRate,
                Parameters = new Dictionary<string, object> { { "IntervalSeconds", 2.0 } }
            };

            var method = typeof(SensorManager).GetMethod("HandleSensorCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method.Invoke(_sensorManager, new object[] { new MessageEnvelope<SensorCommandMessage>(startMsg, MessageOrigins.SensorManager) });
            Assert.IsTrue(((TestSensor)_sensors[0]).Started);

            method.Invoke(_sensorManager, new object[] { new MessageEnvelope<SensorCommandMessage>(stopMsg, MessageOrigins.SensorManager) });
            Assert.IsTrue(((TestSensor)_sensors[1]).Stopped);

            method.Invoke(_sensorManager, new object[] { new MessageEnvelope<SensorCommandMessage>(adjustMsg, MessageOrigins.SensorManager) });
            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorCommandAckMessage>));
        }

        [TestMethod]
        public void HandleStateChanged_ShouldPublishErrorOrStatus()
        {
            var method = typeof(SensorManager).GetMethod("HandleStateChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var testSensor = (TestSensor)_sensors[0];

            method.Invoke(_sensorManager, new object[] { testSensor, SensorStates.Error });
            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorErrorMessage>));

            method.Invoke(_sensorManager, new object[] { testSensor, SensorStates.Running });
            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorStatusMessage>));
        }

        [TestMethod]
        public void Dispose_ShouldUnsubscribeAndDispose()
        {
            _sensorManager.Dispose();
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<SensorCommandMessage>>>()), Times.Once);
        }

        [TestMethod]
        public void EmitSensorInventoryMessage_ShouldPublishInventory()
        {
            _sensorManager.EmitSensorInventoryMessage();
            Assert.IsTrue(_publishedMessages.Any(m => m is MessageEnvelope<SensorInventoryMessage>));
        }
    }
}