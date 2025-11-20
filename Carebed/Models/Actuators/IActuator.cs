using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;

namespace Carebed.Models.Actuators
{
    public interface IActuator
    {
        /// <summary>
        /// A unique identifier for this actuator instance (e.g., "bedLift1", "headTiltA").
        /// Used for routing commands and logging.
        /// </summary>
        string ActuatorId { get; }

        /// <summary>
        /// The type of actuator, represented as an enum (e.g., BedLift, HeadTilt, LegRaise).
        /// Enables classification and filtering.
        /// </summary>
        ActuatorType Type { get; }

        /// <summary>
        /// The current state of the actuator (e.g., Idle, Moving, Locked, Error).
        /// Backed by an internal state machine to enforce valid transitions.
        /// </summary>
        ActuatorState CurrentState { get; }

        /// <summary>
        /// Attempts to execute a command on the actuator.
        /// Returns true if the command is accepted and initiated, false if rejected (e.g., due to invalid state).
        /// </summary>
        bool TryExecute(ActuatorCommand command);

        /// <summary>
        /// Event triggered when the actuator transitions to a new state.
        /// Useful for emitting status messages or updating the UI.
        /// </summary>
        event Action<ActuatorState> OnStateChanged;

        /// <summary>
        /// Returns telemetry data specific to this actuator (e.g., position, load, temperature).
        /// Optional: may return null or a default object if telemetry is not supported.
        /// </summary>
        ActuatorTelemetryMessage? GetTelemetry();

        /// <summary>
        /// Resets the actuator to a known safe state (e.g., from Error to Idle).
        /// Optional: may be a no-op for stateless or simple actuators.
        /// </summary>
        void Reset();
    }
}
