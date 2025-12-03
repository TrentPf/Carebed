using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.Message.AlertMessages;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Carebed.Tests.Managers
{
    [TestClass]
    public class AlertManagerTests
    {
        private Mock<IEventBus> _eventBusMock;
        private AlertManager _alertManager;
        private List<object> _publishedMessages;

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
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertClearAckMessage>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<SensorTelemetryMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<SensorErrorMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<SensorStatusMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<ActuatorErrorMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<ActuatorTelemetryMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _eventBusMock
                .Setup(x => x.PublishAsync(It.IsAny<MessageEnvelope<AlertActionMessage<ActuatorStatusMessage>>>()))
                .Callback<object>(msg => _publishedMessages.Add(msg))
                .Returns(Task.CompletedTask);
            _alertManager = new AlertManager(_eventBusMock.Object);
        }

        [TestMethod]
        public async Task HandleAlertClear_ClearAllMessages_ShouldClearAllAndAck()
        {
            // Arrange: start manager and create sensor + actuator alerts
            _alertManager.Start();

            var sensorMsg = new SensorErrorMessage
            {
                SensorID = "sensorAll1",
                TypeOfSensor = SensorTypes.HeartRate,
                ErrorCode = SensorErrorCodes.SensorDisconnected,
                Description = "Sensor disconnected",
                CurrentState = SensorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var sensorEnv = new MessageEnvelope<SensorErrorMessage>(sensorMsg, MessageOrigins.SensorManager, MessageTypes.SensorError);

            var methodSensor = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            methodSensor.MakeGenericMethod(typeof(SensorErrorMessage)).Invoke(_alertManager, new object[] { sensorEnv });

            var actuatorMsg = new ActuatorErrorMessage
            {
                ActuatorId = "actAll1",
                TypeOfActuator = ActuatorTypes.BedLift,
                ErrorCode = "E99",
                Description = "Actuator fault",
                CurrentState = ActuatorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var actuatorEnv = new MessageEnvelope<ActuatorErrorMessage>(actuatorMsg, MessageOrigins.ActuatorManager, MessageTypes.ActuatorError);
            var methodAct = typeof(AlertManager).GetMethod("HandleActuatorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            methodAct.MakeGenericMethod(typeof(ActuatorErrorMessage)).Invoke(_alertManager, new object[] { actuatorEnv });

            // Sanity: private store contains alerts
            var activeField = typeof(AlertManager).GetField("_activeAlerts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeDict = (Dictionary<MessageOrigins, Dictionary<string, IEventMessage>>)activeField.GetValue(_alertManager);
            Assert.IsTrue(activeDict.TryGetValue(MessageOrigins.SensorManager, out var sdict) && sdict.Count > 0, "Sensor alerts should be stored.");
            Assert.IsTrue(activeDict.TryGetValue(MessageOrigins.ActuatorManager, out var adict) && adict.Count > 0, "Actuator alerts should be stored.");

            // Clear published messages captured
            _publishedMessages.Clear();

            // Act: send clearAllMessages = true (payload must be non-null per implementation)
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                Source = "ALL",
                Payload = sensorMsg,
                clearAllMessages = true
            };
            var clearEnv = new MessageEnvelope<AlertClearMessage<IEventMessage>>(clearMsg, MessageOrigins.DisplayManager, MessageTypes.AlertClear);
            var clearMethod = typeof(AlertManager).GetMethod("HandleAlertClear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clearMethod.Invoke(_alertManager, new object[] { clearEnv });

            // Assert: ack published with Source == "ALL" and alertCleared == true
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertClearAckMessage>));
            var ackObj = _publishedMessages.Find(m => m is MessageEnvelope<AlertClearAckMessage>) as MessageEnvelope<AlertClearAckMessage>;
            Assert.IsNotNull(ackObj, "AlertClearAck should be published.");
            Assert.AreEqual("ALL", ackObj.Payload.Source);
            Assert.IsTrue(ackObj.Payload.alertCleared);

            // Assert: internal store cleared
            var activeAfter = (Dictionary<MessageOrigins, Dictionary<string, IEventMessage>>)activeField.GetValue(_alertManager);
            Assert.AreEqual(0, activeAfter.Count, "All active alert dictionaries should be cleared when clearAllMessages is true.");
        }

        [TestMethod]
        public void Start_ShouldSubscribeToAllHandlers()
        {
            _alertManager.Start();
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<SensorTelemetryMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<SensorStatusMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<SensorErrorMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<ActuatorTelemetryMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<ActuatorStatusMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<ActuatorErrorMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Subscribe(It.IsAny<Action<MessageEnvelope<AlertClearMessage<IEventMessage>>>>()), Times.Once);
        }

        [TestMethod]
        public void Stop_ShouldUnsubscribeFromAllHandlers()
        {
            _alertManager.Stop();
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<SensorTelemetryMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<SensorStatusMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<SensorErrorMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<ActuatorTelemetryMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<ActuatorStatusMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<ActuatorErrorMessage>>>()), Times.Once);
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<AlertClearMessage<IEventMessage>>>>()), Times.Once);
        }

        [TestMethod]
        public void Dispose_ShouldCallStop()
        {
            _alertManager.Dispose();
            _eventBusMock.Verify(x => x.Unsubscribe(It.IsAny<Action<MessageEnvelope<SensorTelemetryMessage>>>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleSensorErrorMessage_ShouldPublishAlert()
        {
            var msg = new SensorErrorMessage
            {
                SensorID = "sensor1",
                TypeOfSensor = SensorTypes.HeartRate,
                ErrorCode = SensorErrorCodes.SensorDisconnected,
                Description = "Sensor disconnected",
                CurrentState = SensorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<SensorErrorMessage>(msg, MessageOrigins.SensorManager, MessageTypes.SensorError);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(SensorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<SensorErrorMessage>>));
        }

        [TestMethod]
        public async Task HandleSensorStatusMessage_ErrorState_ShouldPublishAlert()
        {
            var msg = new SensorStatusMessage
            {
                SensorID = "sensor2",
                TypeOfSensor = SensorTypes.Temperature,
                CurrentState = SensorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<SensorStatusMessage>(msg, MessageOrigins.SensorManager, MessageTypes.SensorStatus);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(SensorStatusMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<SensorStatusMessage>>));
        }

        [TestMethod]
        public async Task HandleSensorTelemetryMessage_Critical_ShouldPublishAlert()
        {
            var msg = new SensorTelemetryMessage
            {
                SensorID = "sensor3",
                TypeOfSensor = SensorTypes.BloodOxygen,
                Data = new SensorData { Value = 10, Source = "sensor3", SensorType = SensorTypes.BloodOxygen, IsCritical = true },
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<SensorTelemetryMessage>(msg, MessageOrigins.SensorManager, MessageTypes.SensorData);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(SensorTelemetryMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<SensorTelemetryMessage>>));
        }

        [TestMethod]
        public async Task HandleActuatorErrorMessage_ShouldPublishAlert()
        {
            var msg = new ActuatorErrorMessage
            {
                ActuatorId = "act1",
                TypeOfActuator = ActuatorTypes.BedLift,
                ErrorCode = "E01",
                Description = "Actuator jammed",
                CurrentState = ActuatorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<ActuatorErrorMessage>(msg, MessageOrigins.ActuatorManager, MessageTypes.ActuatorError);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleActuatorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(ActuatorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<ActuatorErrorMessage>>));
        }

        [TestMethod]
        public async Task HandleActuatorStatusMessage_ErrorState_ShouldPublishAlert()
        {
            var msg = new ActuatorStatusMessage
            {
                ActuatorId = "act2",
                TypeOfActuator = ActuatorTypes.HeadTilt,
                CurrentState = ActuatorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<ActuatorStatusMessage>(msg, MessageOrigins.ActuatorManager, MessageTypes.ActuatorStatus);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleActuatorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(ActuatorStatusMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<ActuatorStatusMessage>>));
        }

        [TestMethod]
        public async Task HandleActuatorTelemetryMessage_Critical_ShouldPublishAlert()
        {
            var msg = new ActuatorTelemetryMessage
            {
                ActuatorId = "act3",
                TypeOfActuator = ActuatorTypes.BedLift,
                IsCritical = true,
                ErrorCode = "E02",
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<ActuatorTelemetryMessage>(msg, MessageOrigins.ActuatorManager, MessageTypes.ActuatorTelemetry);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleActuatorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(ActuatorTelemetryMessage)).Invoke(_alertManager, new object[] { envelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertActionMessage<ActuatorTelemetryMessage>>));
        }

        [TestMethod]
        public async Task HandleAlertClear_SensorAlert_ShouldRemoveAndAck()
        {
            // First, trigger an alert
            var msg = new SensorErrorMessage
            {
                SensorID = "sensor4",
                TypeOfSensor = SensorTypes.HeartRate,
                ErrorCode = SensorErrorCodes.SensorDisconnected,
                Description = "Sensor disconnected",
                CurrentState = SensorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<SensorErrorMessage>(msg, MessageOrigins.SensorManager, MessageTypes.SensorError);
            _alertManager.Start();
            var method = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(SensorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            // Now clear the alert
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                Source = "sensor4",
                Payload = msg,
                alertNumber = 1
            };
            var clearEnvelope = new MessageEnvelope<AlertClearMessage<IEventMessage>>(clearMsg, MessageOrigins.AlertManager, MessageTypes.AlertClear);
            _eventBusMock.Invocations.Clear();
            var clearMethod = typeof(AlertManager).GetMethod("HandleAlertClear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clearMethod.Invoke(_alertManager, new object[] { clearEnvelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertClearAckMessage>));
        }

        [TestMethod]
        public async Task HandleAlertClear_ActuatorAlert_ShouldRemoveAndAck()
        {
            // First, trigger an alert
            var msg = new ActuatorErrorMessage
            {
                ActuatorId = "act4",
                TypeOfActuator = ActuatorTypes.BedLift,
                ErrorCode = "E03",
                Description = "Actuator error",
                CurrentState = ActuatorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<ActuatorErrorMessage>(msg, MessageOrigins.ActuatorManager, MessageTypes.ActuatorError);
            _alertManager.Start();
            var method = typeof(AlertManager).GetMethod("HandleActuatorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(ActuatorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            // Now clear the alert
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                Source = "act4",
                Payload = msg,
                alertNumber = 1
            };
            var clearEnvelope = new MessageEnvelope<AlertClearMessage<IEventMessage>>(clearMsg, MessageOrigins.AlertManager, MessageTypes.AlertClear);
            _eventBusMock.Invocations.Clear();
            var clearMethod = typeof(AlertManager).GetMethod("HandleAlertClear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clearMethod.Invoke(_alertManager, new object[] { clearEnvelope });
            Assert.IsTrue(_publishedMessages.Exists(m => m is MessageEnvelope<AlertClearAckMessage>));
        }

        [TestMethod]
        public async Task HandleAlertClear_AlertNotFound_ShouldAckWithNotFound()
        {
            var msg = new SensorErrorMessage
            {
                SensorID = "sensorX",
                TypeOfSensor = SensorTypes.HeartRate,
                ErrorCode = SensorErrorCodes.SensorDisconnected,
                Description = "Sensor disconnected",
                CurrentState = SensorStates.Error,
                CorrelationId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                Source = "sensorX",
                Payload = msg,
                alertNumber = 99
            };
            var clearEnvelope = new MessageEnvelope<AlertClearMessage<IEventMessage>>(clearMsg, MessageOrigins.AlertManager, MessageTypes.AlertClear);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var clearMethod = typeof(AlertManager).GetMethod("HandleAlertClear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clearMethod.Invoke(_alertManager, new object[] { clearEnvelope });
            Assert.IsTrue(_publishedMessages.Exists(m =>
                m is MessageEnvelope<AlertClearAckMessage> ackEnv &&
                !((AlertClearAckMessage)ackEnv.Payload).alertCleared));
        }

        [TestMethod]
        public async Task DuplicateAlert_ShouldNotPublishAgain()
        {
            var correlationId = Guid.NewGuid();
            var msg = new SensorErrorMessage
            {
                SensorID = "sensorDup",
                TypeOfSensor = SensorTypes.HeartRate,
                ErrorCode = SensorErrorCodes.SensorDisconnected,
                Description = "Sensor disconnected",
                CurrentState = SensorStates.Error,
                CorrelationId = correlationId,
                CreatedAt = DateTime.UtcNow
            };
            var envelope = new MessageEnvelope<SensorErrorMessage>(msg, MessageOrigins.SensorManager, MessageTypes.SensorError);
            _alertManager.Start();
            _eventBusMock.Invocations.Clear();
            var method = typeof(AlertManager).GetMethod("HandleSensorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.MakeGenericMethod(typeof(SensorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            int countAfterFirst = _publishedMessages.Count;
            // Try to publish the same alert again
            method.MakeGenericMethod(typeof(SensorErrorMessage)).Invoke(_alertManager, new object[] { envelope });
            int countAfterSecond = _publishedMessages.Count;
            Assert.AreEqual(countAfterFirst, countAfterSecond, "Duplicate alert should not be published again.");
        }
    }
}