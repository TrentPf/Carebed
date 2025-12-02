using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Logging;
using Carebed.Infrastructure.Message.LoggerMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Carebed.Tests.Managers
{
    [TestClass]
    public class LoggingManagerTests
    {
        private Mock<IFileLoggingService> _mockLoggingService;
        private Mock<IEventBus> _mockEventBus;
        private LoggingManager _manager;

        [TestInitialize]
        public void Setup()
        {
            _mockLoggingService = new Mock<IFileLoggingService>();
            _mockEventBus = new Mock<IEventBus>();
            _manager = new LoggingManager("logs", "log.txt", _mockLoggingService.Object, _mockEventBus.Object);
        }

        [TestMethod]
        public void IsValidFilePath_ReturnsFalse_ForNullOrWhitespace()
        {
            Assert.IsFalse(LoggingManager.IsValidFilePath(null));
            Assert.IsFalse(LoggingManager.IsValidFilePath(""));
            Assert.IsFalse(LoggingManager.IsValidFilePath("   "));
        }

        [TestMethod]
        public void IsValidFilePath_ReturnsFalse_ForInvalidChars()
        {
            var invalid = "invalid|file.txt";
            Assert.IsFalse(LoggingManager.IsValidFilePath(invalid));
        }

        [TestMethod]
        public void IsValidFilePath_ReturnsTrue_ForValidPath()
        {
            var valid = "validfile.txt";
            Assert.IsTrue(LoggingManager.IsValidFilePath(valid));
        }

        [TestMethod]
        public void UpdateLogLocation_CreatesDirectoryAndChangesFilePath()
        {
            var dir = "testlogs";
            var file = "testlog.txt";
            _mockLoggingService.Setup(s => s.ChangeFilePath(It.IsAny<string>())).Returns(true);

            var result = _manager.UpdateLogLocation(dir, file);

            Assert.IsTrue(result);
            _mockLoggingService.Verify(s => s.ChangeFilePath(System.IO.Path.Combine(dir, file)), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void UpdateLogLocation_Throws_ForInvalidFilePath()
        {
            _manager.UpdateLogLocation("logs", "invalid|file.txt");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void UpdateLogLocation_Throws_WhenLoggerStarted()
        {
            // Simulate logger started
            _mockLoggingService.Setup(s => s.ChangeFilePath(It.IsAny<string>())).Returns(true);
            _manager.Start();
            _manager.UpdateLogLocation("logs", "log.txt");
        }

        [TestMethod]
        public void HandleLogMessage_DelegatesToLoggingService()
        {
            var envelope = new Mock<IMessageEnvelope>().Object;
            _manager.HandleLogMessage(envelope);
            _mockLoggingService.Verify(s => s.Log(envelope), Times.Once);
        }

        [TestMethod]
        public void Start_CallsLoggingServiceStart()
        {
            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _manager.Start();
            _mockLoggingService.Verify(s => s.Start(), Times.Once);
        }

        [TestMethod]
        public void Stop_CallsLoggingServiceStop()
        {
            _mockLoggingService.Setup(s => s.Stop()).Returns(Task.CompletedTask);
            _manager.Start();
            _manager.Stop();
            _mockLoggingService.Verify(s => s.Stop(), Times.Once);
        }

        [TestMethod]
        public void Dispose_CallsLoggingServiceDispose()
        {
            _mockLoggingService.Setup(s => s.Dispose());
            _manager.Dispose();
            _mockLoggingService.Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task HandleLogCommand_StartCommand_InvokesStart()
        {
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Start),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            _mockLoggingService.Verify(s => s.Start(), Times.Once);
            _mockEventBus.Verify(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleLogCommand_StopCommand_InvokesStop()
        {
            // Start first
            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _manager.Start();

            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Stop),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            _mockLoggingService.Setup(s => s.Stop()).Returns(Task.CompletedTask);
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            _mockLoggingService.Verify(s => s.Stop(), Times.Once);
            _mockEventBus.Verify(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleLogCommand_AdjustFilePath_InvokesUpdateLogLocation()
        {
            var metadata = new Dictionary<string, string>
            {
                { "LogDirectory", "newdir" },
                { "FilePath", "newfile.txt" }
            };
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.AdjustLogFilePath, metadata),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            _mockLoggingService.Setup(s => s.ChangeFilePath(It.IsAny<string>())).Returns(true);
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            _mockLoggingService.Verify(s => s.ChangeFilePath(System.IO.Path.Combine("newdir", "newfile.txt")), Times.Once);
            _mockEventBus.Verify(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()), Times.Once);
        }

        [TestMethod]
        public async Task HandleLogCommand_StartCommand_SendsAckMessage()
        {
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Start),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            _mockEventBus.Verify(b => b.PublishAsync(It.Is<MessageEnvelope<LoggerCommandAckMessage>>(ack =>
                ack.Payload.CommandType == LoggerCommands.Start)), Times.Once);
        }

        [TestMethod]
        public async Task HandleLogCommand_StopCommand_SendsAckMessage()
        {
            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _manager.Start();

            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Stop),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            _mockLoggingService.Setup(s => s.Stop()).Returns(Task.CompletedTask);
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            _mockEventBus.Verify(b => b.PublishAsync(It.Is<MessageEnvelope<LoggerCommandAckMessage>>(ack =>
                ack.Payload.CommandType == LoggerCommands.Stop)), Times.Once);
        }

        [TestMethod]
        public async Task LoggingManager_CreatesLogFile_AtCorrectPath()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var logFile = "testlog.txt";
            var logPath = Path.Combine(tempDir, logFile);
            try
            {
                var logger = new SimpleFileLogger(logPath);
                var eventBus = new Mock<IEventBus>().Object;
                var manager = new LoggingManager(tempDir, logFile, logger, eventBus);
                manager.Start();
                manager.Stop();
                Assert.IsTrue(File.Exists(logPath), $"Log file was not created at {logPath}");
            }
            finally
            {
                if (File.Exists(logPath)) File.Delete(logPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
            }
        }

        [TestMethod]
        public async Task LoggingManager_WritesLogEntry_ToLogFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var logFile = "testlog.txt";
            var logPath = Path.Combine(tempDir, logFile);
            try
            {
                var logger = new SimpleFileLogger(logPath);
                var eventBus = new Mock<IEventBus>().Object;
                var manager = new LoggingManager(tempDir, logFile, logger, eventBus);
                manager.Start();
                var envelopeMock = new Mock<IMessageEnvelope>();
                envelopeMock.Setup(e => e.MessageId).Returns(Guid.NewGuid());
                envelopeMock.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
                envelopeMock.Setup(e => e.MessageOrigin).Returns(MessageOrigins.LoggingManager);
                envelopeMock.Setup(e => e.MessageType).Returns(MessageTypes.LoggerCommandResponse);
                envelopeMock.Setup(e => e.Payload).Returns("TestPayload");
                manager.HandleLogMessage(envelopeMock.Object);
                await Task.Delay(300); // Allow background worker to process
                manager.Stop();
                var fileContent = File.ReadAllText(logPath);
                Assert.IsTrue(fileContent.Contains("TestPayload"), "Log entry was not written to the log file.");
            }
            finally
            {
                if (File.Exists(logPath)) File.Delete(logPath);
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
            }
        }

        [TestMethod]
        public async Task HandleLogCommand_GetLogFilePath_SendsAckWithCorrectFilePath()
        {
            // Arrange
            var logDir = "logs";
            var logFile = "log.txt";
            var expectedPath = System.IO.Path.Combine(logDir, logFile);

            // Setup the LoggingManager with known logDir and logFile
            var manager = new LoggingManager(logDir, logFile, _mockLoggingService.Object, _mockEventBus.Object);

            LoggerCommandAckMessage receivedAck = null;
            _mockEventBus
                .Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Callback<IMessageEnvelope>(env =>
                {
                    var ackEnvelope = env as MessageEnvelope<LoggerCommandAckMessage>;
                    if (ackEnvelope != null)
                        receivedAck = ackEnvelope.Payload;
                })
                .Returns(Task.CompletedTask);

            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.GetLogFilePath),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var task = (Task)handleLogCommand.Invoke(manager, new object[] { envelope });
            await task;

            // Assert
            Assert.IsNotNull(receivedAck, "No LoggerCommandAckMessage was published.");
            Assert.IsTrue(receivedAck.IsAcknowledged, "The ack message was not acknowledged.");
            Assert.IsNotNull(receivedAck.Metadata, "Metadata should not be null.");
            Assert.IsTrue(receivedAck.Metadata.ContainsKey("FilePath"), "Metadata does not contain 'FilePath'.");
            Assert.AreEqual(expectedPath, receivedAck.Metadata["FilePath"], "FilePath in ack does not match expected.");
        }

        [TestMethod]
        public async Task HandleLogCommand_StartCommand_WhenAlreadyStarted_SendsNegativeAck()
        {
            // Arrange
            _mockLoggingService.Setup(s => s.Start()).Returns(Task.CompletedTask);
            _manager.Start();

            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Start),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            MessageEnvelope<LoggerCommandAckMessage> receivedEnvelope = null;
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Callback<MessageEnvelope<LoggerCommandAckMessage>>(env => receivedEnvelope = env)
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            // Assert
            Assert.IsNotNull(receivedEnvelope, "No ack message was published.");
            Assert.IsFalse(receivedEnvelope.Payload.IsAcknowledged, "Ack should indicate failure when already started.");
            Assert.AreEqual(LoggerCommands.Start, receivedEnvelope.Payload.CommandType);
        }

        [TestMethod]
        public async Task HandleLogCommand_StopCommand_WhenAlreadyStopped_SendsNegativeAck()
        {
            // Arrange: Ensure logger is stopped
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.Stop),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            MessageEnvelope<LoggerCommandAckMessage> receivedEnvelope = null;
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Callback<MessageEnvelope<LoggerCommandAckMessage>>(env => receivedEnvelope = env)
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;

            // Assert
            Assert.IsNotNull(receivedEnvelope, "No ack message was published.");
            Assert.IsFalse(receivedEnvelope.Payload.IsAcknowledged, "Ack should indicate failure when already stopped.");
            Assert.AreEqual(LoggerCommands.Stop, receivedEnvelope.Payload.CommandType);
        }

        [TestMethod]
        public async Task HandleLogCommand_AdjustLogFilePath_WithInvalidFilePath_SendsNegativeAck()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                { "LogDirectory", "logs" },
                { "FilePath", "invalid|file.txt" }
            };
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(LoggerCommands.AdjustLogFilePath, metadata),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            MessageEnvelope<LoggerCommandAckMessage> receivedEnvelope = null;
            _mockEventBus.Setup(b => b.PublishAsync(It.IsAny<MessageEnvelope<LoggerCommandAckMessage>>()))
                .Callback<MessageEnvelope<LoggerCommandAckMessage>>(env => receivedEnvelope = env)
                .Returns(Task.CompletedTask);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
                await task;
            });
        }

        [TestMethod]
        public async Task HandleLogCommand_UnknownCommand_DoesNotThrow()
        {
            // Arrange: Use an undefined enum value
            var unknownCommand = (LoggerCommands)9999;
            var envelope = new MessageEnvelope<LoggerCommandMessage>(
                new LoggerCommandMessage(unknownCommand),
                MessageOrigins.LoggingManager,
                MessageTypes.LoggerCommandResponse);

            var handleLogCommand = typeof(LoggingManager)
                .GetMethod("HandleLogCommand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert: Should not throw
            var task = (Task)handleLogCommand.Invoke(_manager, new object[] { envelope });
            await task;
        }
    }
}
