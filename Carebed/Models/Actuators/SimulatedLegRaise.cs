using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;
using System;
using System.Collections.Generic;

namespace Carebed.Models.Actuators
{
    public class SimulatedLegRaise : ActuatorBase
    {
        private double _extension = 0.0; // 0..50 cm
        private DateTime? _moveTimestamp = null;
        private ActuatorCommands? _currentMotion = null;
        private const double _cmPerSecond = 1.0;

        public SimulatedLegRaise(string actuatorId) : base(actuatorId, ActuatorTypes.LegRaise, GetTransitionMap())
        {
        }

        private static Dictionary<ActuatorStates, ActuatorStates[]> GetTransitionMap()
        {
            return new Dictionary<ActuatorStates, ActuatorStates[]>
            {
                { ActuatorStates.Idle, new[] { ActuatorStates.Moving, ActuatorStates.Locked, ActuatorStates.Error } },
                { ActuatorStates.Moving, new[] { ActuatorStates.Completed, ActuatorStates.Idle, ActuatorStates.Error } },
                { ActuatorStates.Completed, new[] { ActuatorStates.Idle, ActuatorStates.Error } },
                { ActuatorStates.Locked, new[] { ActuatorStates.Idle } },
                { ActuatorStates.Error, new[] { ActuatorStates.Idle } }
            };
        }

        public override bool TryExecute(ActuatorCommands command)
        {
            switch (command)
            {
                case ActuatorCommands.Raise:
                    if (TryTransition(ActuatorStates.Moving))
                    {
                        _currentMotion = ActuatorCommands.Raise;
                        _moveTimestamp = DateTime.UtcNow;
                        return true;
                    }
                    return false;

                case ActuatorCommands.Lower:
                    if (TryTransition(ActuatorStates.Moving))
                    {
                        _currentMotion = ActuatorCommands.Lower;
                        _moveTimestamp = DateTime.UtcNow;
                        return true;
                    }
                    return false;

                case ActuatorCommands.Stop:
                    if (CurrentState == ActuatorStates.Moving)
                    {
                        UpdateExtension();
                        _currentMotion = null;
                        _moveTimestamp = null;
                        TryTransition(ActuatorStates.Completed);
                        return true;
                    }
                    return false;

                case ActuatorCommands.Lock:
                    return TryTransition(ActuatorStates.Locked);

                case ActuatorCommands.Unlock:
                    return TryTransition(ActuatorStates.Idle);

                case ActuatorCommands.Reset:
                    Reset();
                    return true;

                default:
                    return false;
            }
        }

        private void UpdateExtension()
        {
            if (!_moveTimestamp.HasValue || !_currentMotion.HasValue)
                return;

            var seconds = (DateTime.UtcNow - _moveTimestamp.Value).TotalSeconds;
            var delta = seconds * _cmPerSecond;

            if (_currentMotion == ActuatorCommands.Raise)
            {
                _extension = Math.Min(50.0, _extension + delta);
            }
            else if (_currentMotion == ActuatorCommands.Lower)
            {
                _extension = Math.Max(0.0, _extension - delta);
            }

            _moveTimestamp = DateTime.UtcNow;
        }

        public override ActuatorTelemetryMessage GetTelemetry()
        {
            if (CurrentState == ActuatorStates.Moving)
            {
                UpdateExtension();
            }

            return new ActuatorTelemetryMessage
            {
                ActuatorId = ActuatorId,
                TypeOfActuator = Type,
                Position = new ActuatorPosition { Extension = _extension },
                Load = CurrentState == ActuatorStates.Moving ? 20.0 : 3.0,
                Temperature = 27.5,
                Watts = CurrentState == ActuatorStates.Moving ? 60.0 : 6.0,
                ErrorCode = "NoError",
                IsCritical = false
            };
        }

        public override void Reset()
        {
            _extension = 0.0;
            _moveTimestamp = null;
            _currentMotion = null;
            TryTransition(ActuatorStates.Idle);
        }
    }
}
