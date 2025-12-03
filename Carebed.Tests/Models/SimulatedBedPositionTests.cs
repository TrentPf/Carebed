// Pseudocode / Plan (detailed):
// 1. Create tests for `SimulatedBedPosition` covering common behaviors:
//    - Verify `TryExecute(Raise)` from `Idle` starts `Moving` and telemetry reflects moving load/watts.
//    - Verify that time progression updates `_position` correctly when moving (use reflection to set `_moveTimestamp` to the past).
//    - Verify `Stop` while moving updates position and transitions to `Completed` (telemetry shows non-moving load/watts).
//    - Verify `Raise` clamps at 100% and `Lower` clamps at 0% (simulate large elapsed time).
//    - Verify `Lock` prevents motion and `Unlock` restores `Idle`.
//    - Verify `Reset` clears position and returns state to `Idle`.
// 2. Implement helper methods inside the test class to get/set private fields (`_position`, `_moveTimestamp`, `_currentMotion`) via reflection.
// 3. Use `MSTest` framework for assertions and `[TestMethod]` attributes.
// 4. Keep tests deterministic by controlling `DateTime.UtcNow` effect via setting `_moveTimestamp` to a past value.
// 5. Assert with tolerances for floating point comparisons where appropriate.
// 6. Keep tests isolated: create a fresh `SimulatedBedPosition` instance per test.

using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Carebed.Models.Actuators;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;

namespace Carebed.Tests.Models
{
    [TestClass]
    public class SimulatedBedPositionTests
    {
        private const double Epsilon = 0.0001;
        private const double PositionTolerance = 0.02; // allow small motion due to timing (e.g., ~2%)

        private static FieldInfo GetPrivateField(string name)
        {
            return typeof(SimulatedBedPosition).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Field '{name}' not found on SimulatedBedPosition.");
        }

        private static void SetPrivateField<T>(SimulatedBedPosition instance, string name, T value)
        {
            var fi = GetPrivateField(name);
            fi.SetValue(instance, value);
        }

        private static T GetPrivateFieldValue<T>(SimulatedBedPosition instance, string name)
        {
            var fi = GetPrivateField(name);
            return (T)fi.GetValue(instance)!;
        }

        [TestMethod]
        public void TryExecute_Raise_FromIdle_ShouldEnterMovingAndTelemetryReflectsMoving()
        {
            var actuator = new SimulatedBedPosition("bed1");

            // initial state should be Idle (or transitioning to Idle)
            Assert.AreEqual(ActuatorStates.Idle, actuator.CurrentState);

            var accepted = actuator.TryExecute(ActuatorCommands.Raise);
            Assert.IsTrue(accepted, "Raise should be accepted from Idle.");
            Assert.AreEqual(ActuatorStates.Moving, actuator.CurrentState, "State should be Moving after Raise.");

            var telemetry = actuator.GetTelemetry();
            Assert.IsNotNull(telemetry);
            Assert.AreEqual(12.5, telemetry.Load ?? 0.0, Epsilon, "Load should reflect moving state.");
            Assert.AreEqual(40.0, telemetry.Watts ?? 0.0, Epsilon, "Watts should reflect moving state.");
            Assert.IsNotNull(telemetry.Position);
            Assert.AreEqual(0.0, telemetry.Position.Extension ?? 0.0, PositionTolerance, "Position should start near 0 immediately after starting motion.");
        }

        [TestMethod]
        public void Moving_UpdatePosition_AppliesElapsedTimeAndStopTransitionsToCompleted()
        {
            var actuator = new SimulatedBedPosition("bed2");

            // start from a known base position
            SetPrivateField(actuator, "_position", 10.0);
            var startAccepted = actuator.TryExecute(ActuatorCommands.Raise);
            Assert.IsTrue(startAccepted);
            Assert.AreEqual(ActuatorStates.Moving, actuator.CurrentState);

            // Simulate 2 seconds elapsed by setting _moveTimestamp to 2 seconds in the past
            SetPrivateField(actuator, "_moveTimestamp", DateTime.UtcNow.AddSeconds(-2));
            // Ensure _currentMotion is Raise (it should be set by TryExecute, but ensure via reflection)
            SetPrivateField(actuator, "_currentMotion", ActuatorCommands.Raise);

            // Stop should update position and transition to Completed
            var stopAccepted = actuator.TryExecute(ActuatorCommands.Stop);
            Assert.IsTrue(stopAccepted, "Stop should be accepted when Moving.");
            Assert.AreEqual(ActuatorStates.Completed, actuator.CurrentState, "State should be Completed after Stop.");

            // Telemetry should now indicate non-moving load/watts and updated position
            var telemetry = actuator.GetTelemetry();
            Assert.AreEqual(2.0, telemetry.Load ?? 0.0, Epsilon, "Load should reflect non-moving state after Stop.");
            Assert.AreEqual(5.0, telemetry.Watts ?? 0.0, Epsilon, "Watts should reflect non-moving state after Stop.");
            Assert.IsNotNull(telemetry.Position);

            // Expected delta: 2 seconds * 5 %/s = 10 => 10 + initial 10 = 20
            Assert.AreEqual(20.0, telemetry.Position.Extension ?? 0.0, 0.01, "Position should have incremented by approx 10% from initial 10%.");
        }

        [TestMethod]
        public void Raise_ClampsTo100_WhenElapsedLongEnough()
        {
            var actuator = new SimulatedBedPosition("bed3");

            // start near the top
            SetPrivateField(actuator, "_position", 98.0);
            var accepted = actuator.TryExecute(ActuatorCommands.Raise);
            Assert.IsTrue(accepted);

            // simulate long elapsed time (e.g., 10 seconds => delta 50% -> 98 + 50 = 148 -> clamp to 100)
            SetPrivateField(actuator, "_moveTimestamp", DateTime.UtcNow.AddSeconds(-10));
            SetPrivateField(actuator, "_currentMotion", ActuatorCommands.Raise);

            // update via telemetry which calls UpdatePosition internally
            var telemetry = actuator.GetTelemetry();
            Assert.IsNotNull(telemetry.Position);
            Assert.AreEqual(100.0, telemetry.Position.Extension ?? 0.0, 0.01, "Position should be clamped at 100%.");
        }

        [TestMethod]
        public void Lower_ClampsTo0_WhenElapsedLongEnough()
        {
            var actuator = new SimulatedBedPosition("bed4");

            // start near bottom
            SetPrivateField(actuator, "_position", 2.0);
            var accepted = actuator.TryExecute(ActuatorCommands.Lower);
            Assert.IsTrue(accepted);

            // simulate long elapsed time (e.g., 10 seconds => delta 50% -> 2 - 50 = -48 -> clamp to 0)
            SetPrivateField(actuator, "_moveTimestamp", DateTime.UtcNow.AddSeconds(-10));
            SetPrivateField(actuator, "_currentMotion", ActuatorCommands.Lower);

            var telemetry = actuator.GetTelemetry();
            Assert.IsNotNull(telemetry.Position);
            Assert.AreEqual(0.0, telemetry.Position.Extension ?? 0.0, 0.01, "Position should be clamped at 0%.");
        }

        [TestMethod]
        public void Lock_PreventsMotion_AndUnlockRestoresIdle()
        {
            var actuator = new SimulatedBedPosition("bed5");

            // Lock from Idle
            var lockAccepted = actuator.TryExecute(ActuatorCommands.Lock);
            Assert.IsTrue(lockAccepted);
            Assert.AreEqual(ActuatorStates.Locked, actuator.CurrentState, "Actuator should be Locked after Lock command.");

            // Raise should be rejected when locked
            var raiseAccepted = actuator.TryExecute(ActuatorCommands.Raise);
            Assert.IsFalse(raiseAccepted, "Raise should be rejected when actuator is Locked.");

            // Unlock should transition back to Idle
            var unlockAccepted = actuator.TryExecute(ActuatorCommands.Unlock);
            Assert.IsTrue(unlockAccepted);
            Assert.AreEqual(ActuatorStates.Idle, actuator.CurrentState, "Actuator should return to Idle after Unlock.");
        }

        [TestMethod]
        public void Reset_ClearsPositionAndState()
        {
            var actuator = new SimulatedBedPosition("bed6");

            // set to some state and position
            SetPrivateField(actuator, "_position", 55.0);
            var startAccepted = actuator.TryExecute(ActuatorCommands.Raise);
            Assert.IsTrue(startAccepted);
            Assert.AreEqual(ActuatorStates.Moving, actuator.CurrentState);

            // Call Reset via TryExecute
            var resetAccepted = actuator.TryExecute(ActuatorCommands.Reset);
            Assert.IsTrue(resetAccepted);

            // After reset, position should be 0 and state should be Idle
            var pos = GetPrivateFieldValue<double>(actuator, "_position");
            Assert.AreEqual(0.0, pos, Epsilon, "Reset should set position to 0.");

            Assert.AreEqual(ActuatorStates.Idle, actuator.CurrentState, "Reset should transition to Idle state.");
        }
    }
}