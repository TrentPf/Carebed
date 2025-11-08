/******************************************************************************
 * File: MessageEnvelopeTests.cs
 * Project: Carebed.Tests
 * Description: Unit tests for MessageEnvelope<T> functionality.
 * 
 * Author: Mattthew Schatz
 * Date: November 8, 2025
 * 
 * C# Version: 13.0
 * .NET Target: .NET 8
 * 
 * Copilot AI Acknowledgement:
 *   Some or all of the tests in this file were generated or assisted by GitHub Copilot AI.
 *   Please review and validate for correctness and completeness.
 ******************************************************************************/
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.MessageEnvelope;


namespace Carebed.Tests
{
    [TestClass]
    public class MessageEnvelopeTests
    {
        [TestMethod]
        public void Constructor_SetsPayloadAndTimestamp()
        {
            // Arrange 

            var payload = new SensorData(
                Value: 42.0,
                Source: "Test",
                IsCritical: false,
                Metadata: null
            );
            var envelope = new MessageEnvelope<SensorData>(payload, Infrastructure.Enums.MessageOriginEnum.Unknown);

            // Act & Assert

            // Test if payload is set correctly
            Assert.AreEqual(payload, envelope.Payload);

            // Test that the MessageOrigin is set correctly
            Assert.AreEqual(Infrastructure.Enums.MessageOriginEnum.Unknown, envelope.MessageOrigin);
        }

        [TestMethod]
        public void Constructor_NullPayload_Throws()
        {
            // Arrange
            SensorData? payload = null;

            // Act & Assert
            // Ensure that the function throws an exception because nulls are not allowed
            Assert.ThrowsException<ArgumentNullException>(() => new MessageEnvelope<SensorData>(payload, Infrastructure.Enums.MessageOriginEnum.Unknown));
        }

        [TestMethod]
        public void Constructor_MalformedPayload_StoresPayload()
        {
            var payload = new SensorData(
                Value: double.NaN,
                Source: null,
                IsCritical: false,
                Metadata: null
            );
            var envelope = new MessageEnvelope<SensorData>(payload, Infrastructure.Enums.MessageOriginEnum.Unknown);

            Assert.AreEqual(payload, envelope.Payload);
            Assert.IsNull(envelope.Payload.Source);
            Assert.IsTrue(double.IsNaN(envelope.Payload.Value));
        }
    }
}
