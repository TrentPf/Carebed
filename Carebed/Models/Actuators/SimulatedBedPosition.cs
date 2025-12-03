using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;
using System;
using System.Collections.Generic;

namespace Carebed.Models.Actuators
{
    public class SimulatedBedPosition : ActuatorBase
    {
        private double _position = 0.0; // 0..100% (0 = flat, 100 = fully raised)
        private DateTime? _moveTimestamp = null;
        private ActuatorCommands? _currentMotion = null;
        private const double _speedPercentPerSecond = 5.0; // percent per second

        public SimulatedBedPosition(string actuatorId) : base(actuatorId, ActuatorTypes.BedLift, GetTransitionMap())
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
                        UpdatePosition();
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

        private void UpdatePosition()
        {
            if (!_moveTimestamp.HasValue || !_currentMotion.HasValue)
                return;

            var seconds = (DateTime.UtcNow - _moveTimestamp.Value).TotalSeconds;
            var delta = seconds * _speedPercentPerSecond;

            if (_currentMotion == ActuatorCommands.Raise)
            {
                _position = Math.Min(100.0, _position + delta);
            }
            else if (_currentMotion == ActuatorCommands.Lower)
            {
                _position = Math.Max(0.0, _position - delta);
            }

            // reset timestamp so subsequent telemetry increments from now
            _moveTimestamp = DateTime.UtcNow;
        }

        public override ActuatorTelemetryMessage GetTelemetry()
        {
            if (CurrentState == ActuatorStates.Moving)
            {
                UpdatePosition();
            }

            return new ActuatorTelemetryMessage
            {
                ActuatorId = ActuatorId,
                TypeOfActuator = Type,
                Position = new ActuatorPosition { Extension = _position },
                Load = CurrentState == ActuatorStates.Moving ? 12.5 : 2.0,
                Temperature = 28.0,
                Watts = CurrentState == ActuatorStates.Moving ? 40.0 : 5.0,
                ErrorCode = "NoError",
                IsCritical = false
            };
        }

        public override void Reset()
        {
            _position = 0.0;
            _moveTimestamp = null;
            _currentMotion = null;
            TryTransition(ActuatorStates.Idle);
        }
    }
}
