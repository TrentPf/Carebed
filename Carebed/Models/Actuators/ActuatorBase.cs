using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;

namespace Carebed.Models.Actuators
{
    public abstract class ActuatorBase : IActuator
    {
        /// <summary>
        /// Unique identifier for this actuator instance (e.g., "bedLift1", "headTiltA").
        /// </summary>
        public string ActuatorId { get; }

        /// <summary>
        /// Gets the type of the actuator.
        /// </summary>
        public ActuatorType Type { get; }

        /// <summary>
        /// Gets the current state of the actuator.
        /// </summary>
        public ActuatorState CurrentState => _stateMachine.Current;

        /// <summary>
        /// A state machine to manage the actuator's states and transitions.
        /// </summary>
        protected readonly StateMachine<ActuatorState> _stateMachine;

        /// <summary>
        /// Event triggered when the actuator transitions to a new state.
        /// </summary>
        public event Action<ActuatorState>? OnStateChanged;

        /// <summary>
        /// Constructor for the ActuatorBase class.
        /// </summary>
        protected ActuatorBase(string actuatorId, ActuatorType type, Dictionary<ActuatorState, ActuatorState[]> transitionMap)
        {
            ActuatorId = actuatorId;
            Type = type;
            _stateMachine = new StateMachine<ActuatorState>(ActuatorState.Idle, transitionMap);
        }

        /// <summary>
        /// A method to attempt a state transition in the actuator's state machine.
        /// </summary>
        public bool TryTransition(ActuatorState next)
        {
            if (_stateMachine.TryTransition(next))
            {
                OnStateChanged?.Invoke(_stateMachine.Current);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to execute the specified actuator command.
        /// </summary>
        /// <remarks>
        /// **Must be implemented by derived classes.**
        /// </remarks>
        /// <param name="command">The <see cref="ActuatorCommand"/> to be executed. Cannot be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the command was successfully executed; otherwise, <see langword="false"/>.</returns>
        public abstract bool TryExecute(ActuatorCommand command);

        /// <summary>
        /// Abstract method to get telemetry data specific to this actuator.</summary>
        /// <remarks>
        /// **Must be implemented by derived classes.**
        /// </remarks>        
        public abstract ActuatorTelemetryMessage GetTelemetry();

        /// <summary>
        /// Method to reset the actuator to a known safe state (e.g., from Error to Idle).
        /// </summary>
        public virtual void Reset()
        {
            TryTransition(ActuatorState.Idle);
        }
    }
}
