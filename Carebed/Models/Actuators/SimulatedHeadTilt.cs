using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.ActuatorMessages;
using System;
using System.Collections.Generic;

namespace Carebed.Models.Actuators
{
    public class SimulatedHeadTilt : ActuatorBase
    {
        private double _angle = 0.0; // angle in degrees, -30..+30
        private DateTime? _moveTimestamp = null;
        private ActuatorCommands? _currentMotion = null;
        private const double _degreesPerSecond = 5.0;

        public SimulatedHeadTilt(string actuatorId) : base(actuatorId, ActuatorTypes.HeadTilt, GetTransitionMap())
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
                        _currentMotion = ActuatorCommands.Raise; // treat Raise as tilt forward
                        _moveTimestamp = DateTime.UtcNow;
                        return true;
                    }
                    return false;

                case ActuatorCommands.Lower:
                    if (TryTransition(ActuatorStates.Moving))
                    {
                        _currentMotion = ActuatorCommands.Lower; // treat Lower as tilt backward
                        _moveTimestamp = DateTime.UtcNow;
                        return true;
                    }
                    return false;

                case ActuatorCommands.Stop:
                    if (CurrentState == ActuatorStates.Moving)
                    {
                        UpdateAngle();
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

        private void UpdateAngle()
        {
            if (!_moveTimestamp.HasValue || !_currentMotion.HasValue)
                return;

            var seconds = (DateTime.UtcNow - _moveTimestamp.Value).TotalSeconds;
            var delta = seconds * _degreesPerSecond;

            if (_currentMotion == ActuatorCommands.Raise)
            {
                _angle = Math.Min(30.0, _angle + delta);
            }
            else if (_currentMotion == ActuatorCommands.Lower)
            {
                _angle = Math.Max(-30.0, _angle - delta);
            }

            _moveTimestamp = DateTime.UtcNow;
        }

        public override ActuatorTelemetryMessage GetTelemetry()
        {
            if (CurrentState == ActuatorStates.Moving)
            {
                UpdateAngle();
            }

            return new ActuatorTelemetryMessage
            {
                ActuatorId = ActuatorId,
                TypeOfActuator = Type,
                Position = new ActuatorPosition { Angle = _angle },
                Load = CurrentState == ActuatorStates.Moving ? 6.0 : 1.0,
                Temperature = 26.0,
                Watts = CurrentState == ActuatorStates.Moving ? 20.0 : 2.0,
                ErrorCode = "NoError",
                IsCritical = false
            };
        }

        public override void Reset()
        {
            _angle = 0.0;
            _moveTimestamp = null;
            _currentMotion = null;
            TryTransition(ActuatorStates.Idle);
        }
    }
}
