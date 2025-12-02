using System.Threading.Tasks;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.LoggerMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carebed.Tests.Managers
{
    [TestClass]
    public class LoggerCommandAckMessageEventBusTests
    {
        private class InMemoryEventBus : IEventBus
        {
            private event Action<MessageEnvelope<LoggerCommandAckMessage>> LoggerAckHandlers;

            public void SubscribeToGlobalMessages(Action<IMessageEnvelope> handler) { }
            public void UnsubscribeFromGlobalMessages(Action<IMessageEnvelope> handler) { }

            public void Subscribe<TPayload>(Action<MessageEnvelope<TPayload>> handler) where TPayload : IEventMessage
            {
                if (typeof(TPayload) == typeof(LoggerCommandAckMessage))
                {
                    LoggerAckHandlers += handler as Action<MessageEnvelope<LoggerCommandAckMessage>>;
                }
            }

            public void Unsubscribe<TPayload>(Action<MessageEnvelope<TPayload>> handler) where TPayload : IEventMessage
            {
                if (typeof(TPayload) == typeof(LoggerCommandAckMessage))
                {
                    LoggerAckHandlers -= handler as Action<MessageEnvelope<LoggerCommandAckMessage>>;
                }
            }

            public Task PublishAsync<TPayload>(MessageEnvelope<TPayload> message) where TPayload : IEventMessage
            {
                if (typeof(TPayload) == typeof(LoggerCommandAckMessage))
                {
                    LoggerAckHandlers?.Invoke(message as MessageEnvelope<LoggerCommandAckMessage>);
                }
                return Task.CompletedTask;
            }

            public void Initialize() { }
            public void Shutdown() { }
        }

        [TestMethod]
        public async Task Publish_LoggerCommandAckMessage_ShouldBeReceivedBySubscriber()
        {
            // Arrange
            var eventBus = new InMemoryEventBus();
            bool received = false;
            LoggerCommandAckMessage receivedPayload = null;

            eventBus.Subscribe<LoggerCommandAckMessage>(envelope =>
            {
                received = true;
                receivedPayload = envelope.Payload;
            });

            var ackMessage = new LoggerCommandAckMessage(
                Carebed.Infrastructure.Enums.LoggerCommands.GetLogFilePath,
                isAcknowledged: true,
                reason: null
            );
            var envelope = new MessageEnvelope<LoggerCommandAckMessage>(
                ackMessage,
                Carebed.Infrastructure.Enums.MessageOrigins.LoggingManager,
                Carebed.Infrastructure.Enums.MessageTypes.LoggerCommandResponse
            );

            // Act
            await eventBus.PublishAsync(envelope);

            // Assert
            Assert.IsTrue(received, "The LoggerCommandAckMessage was not received by the subscriber.");
            Assert.IsNotNull(receivedPayload, "The received payload is null.");
            Assert.AreEqual(Carebed.Infrastructure.Enums.LoggerCommands.GetLogFilePath, receivedPayload.CommandType, "CommandType mismatch.");
            Assert.IsTrue(receivedPayload.IsAcknowledged, "IsAcknowledged should be true.");
        }
    }
}