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
        public ActuatorTypes Type { get; }

        /// <summary>
        /// Gets the current state of the actuator.
        /// </summary>
        public ActuatorStates CurrentState => _stateMachine.Current;

        /// <summary>
        /// A state machine to manage the actuator's states and transitions.
        /// </summary>
        protected readonly StateMachine<ActuatorStates> _stateMachine;

        /// <summary>
        /// Event triggered when the actuator transitions to a new state.
        /// </summary>
        public event Action<ActuatorStates>? OnStateChanged;

        public event Action<ActuatorStatusMessage>? OnStatusMessage;

        /// <summary>
        /// Constructor for the ActuatorBase class.
        /// </summary>
        protected ActuatorBase(string actuatorId, ActuatorTypes type, Dictionary<ActuatorStates, ActuatorStates[]> transitionMap, ActuatorStates initialState = ActuatorStates.Idle)
        {
            ActuatorId = actuatorId;
            Type = type;
            _stateMachine = new StateMachine<ActuatorStates>(initialState, transitionMap);
        }

        /// <summary>
        /// A method to attempt a state transition in the actuator's state machine.
        /// </summary>
        public bool TryTransition(ActuatorStates next)
        {
            if (_stateMachine.TryTransition(next))
            {
                OnStateChanged?.Invoke(_stateMachine.Current);
                PublishStatus(); // <-- Ensures status is published after every transition
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
        /// <param name="command">The <see cref="ActuatorCommands"/> to be executed. Cannot be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the command was successfully executed; otherwise, <see langword="false"/>.</returns>
        public abstract bool TryExecute(ActuatorCommands command);

        /// <summary>
        /// Abstract method to get telemetry data specific to this actuator.</summary>
        /// <remarks>
        /// **Must be implemented by derived classes.**
        /// </remarks>        
        public abstract ActuatorTelemetryMessage GetTelemetry();

        /// <summary>
        /// Publishes the current status of the actuator.
        /// </summary>
        public void PublishStatus()
        {
            var statusMsg = new ActuatorStatusMessage
            {
                ActuatorId = ActuatorId,
                TypeOfActuator = Type,
                CurrentState = CurrentState,
                CreatedAt = DateTime.UtcNow
                // Add other fields as needed
            };
            OnStatusMessage?.Invoke(statusMsg);
        }

        /// <summary>
        /// Method to reset the actuator to a known safe state (e.g., from Error to Idle).
        /// </summary>
        public virtual void Reset()
        {
            TryTransition(ActuatorStates.Idle);
        }
    }
}
