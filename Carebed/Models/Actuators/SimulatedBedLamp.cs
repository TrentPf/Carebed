using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;

namespace Carebed.Models.Actuators
{
    public class SimulatedBedLamp : ActuatorBase
    {
        #region Fields
        private DateTime? _onTimestamp = null;

        /// <summary>
        /// Represents the maximum safe temperature for the lamp when it is on.
        /// </summary>
        /// <remarks> When the lamp reaches a temperature higher than this an alert should be triggered by the ActuatorManager.</remarks>
        private double _maxLampTemperature = 50.0; // Maximum safe temperature the lamp can reach when on
        #endregion

        #region Constructor(s)
        public SimulatedBedLamp(string actuatorId): base(actuatorId, ActuatorTypes.Lamp, GetTransitionMap(), ActuatorStates.Off)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Defines the valid state transitions for the simulated bed lamp actuator.
        /// For a binary actuator like a lamp, valid states include On, Off, and Error.
        /// </summary>
        /// <returns>The transition map for the actuator's states.</returns>
        private static Dictionary<ActuatorStates, ActuatorStates[]> GetTransitionMap()
        {
            return new Dictionary<ActuatorStates, ActuatorStates[]>
            {
                { ActuatorStates.Off, new[] { ActuatorStates.On, ActuatorStates.Error } },
                { ActuatorStates.On, new[] { ActuatorStates.Off, ActuatorStates.Error } },
                { ActuatorStates.Error, new[] { ActuatorStates.Off } }
            };
        }

        /// <summary>
        /// Attempts to execute the specified actuator command.
        /// </summary>
        /// <param name="command">The ActuatorCommand to attempt.</param>
        /// <returns>True: if the command was successfully executed; otherwise, returns False.</returns>
        public override bool TryExecute(ActuatorCommands command)
        {
            // Attempt the command by transitioning to the appropriate state
            switch (command)
            {
                case ActuatorCommands.ActivateLamp:
                    if (TryTransition(ActuatorStates.On))
                    {
                        _onTimestamp = DateTime.UtcNow;
                        return true;
                    }

                    return false;

                case ActuatorCommands.DeactivateLamp:
                    if (TryTransition(ActuatorStates.Off))
                    {
                        _onTimestamp = null;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets telemetry data specific to the simulated bed lamp actuator.
        /// </summary>
        /// <remarks>Simulates temperature ramp for lamp, 2°C per second</remarks>
        /// <returns></returns>
        public override ActuatorTelemetryMessage GetTelemetry()
        {
            double temperature = 20.0;
            if (CurrentState == ActuatorStates.On && _onTimestamp.HasValue)
            {
                var secondsOn = (DateTime.UtcNow - _onTimestamp.Value).TotalSeconds;
                temperature = Math.Min(20.0 + secondsOn * 2, 80.0); // Ramps up 2°C per second, max 80°C
            }

            double wattsConsumed = CurrentState == ActuatorStates.On ? 15.0 : 0.0; // Assume lamp consumes 15W when on

            return new ActuatorTelemetryMessage
            {
                ActuatorId = ActuatorId,
                TypeOfActuator = Type,
                Temperature = temperature,
                Watts = wattsConsumed,
                ErrorCode = "NoError",
                IsCritical = temperature > _maxLampTemperature
            };
        }

        public override void Reset()
        {
            // Transition to Off state on reset
            TryTransition(ActuatorStates.Off);
        }

        #endregion
    }
}