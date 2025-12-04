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
        private const double _degreesPerSecond = 33.33;
        private Task? _movementTask;
        private CancellationTokenSource? _movementCts;

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
                    if(TryTransition(ActuatorStates.Moving))
                    {
                        StartMovementTimer(command);
                        return true;
                    }
                    return false;

                case ActuatorCommands.Lower:
                    if (TryTransition(ActuatorStates.Moving))
                    {
                        StartMovementTimer(command);
                        return true;
                    }
                    return false;

                case ActuatorCommands.Stop:
                    CancelMovementTimer();
                    TryTransition(ActuatorStates.Completed);
                    // Schedule transition to Idle after 500ms
                    Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        TryTransition(ActuatorStates.Idle);
                    });
                    return true;

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

        private void StartMovementTimer(ActuatorCommands motion)
        {
            CancelMovementTimer(); // Cancel any previous movement

            _movementCts = new CancellationTokenSource();
            _movementTask = Task.Run(async () =>
            {
                try
                {
                    // Simulate movement duration (e.g., 3 seconds)
                    await Task.Delay(TimeSpan.FromSeconds(3), _movementCts.Token);

                    // Transition to Completed
                    TryTransition(ActuatorStates.Completed);

                    // Short pause before going Idle
                    await Task.Delay(500, _movementCts.Token);

                    TryTransition(ActuatorStates.Idle);
                }
                catch (TaskCanceledException)
                {
                    // Movement was cancelled (e.g., by Stop command)
                }
            }, _movementCts.Token);
        }

        private void CancelMovementTimer()
        {
            if (_movementCts != null)
            {
                _movementCts.Cancel();
                _movementCts.Dispose();
                _movementCts = null;
            }
            _movementTask = null;
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
                if (_angle >= 30.0)
                {
                    _currentMotion = null;
                    _moveTimestamp = null;
                    TryTransition(ActuatorStates.Completed);
                    // Sleep for 500ms, then transition to Idle
                    System.Threading.Thread.Sleep(500);
                    TryTransition(ActuatorStates.Idle);
                }
            }
            else if (_currentMotion == ActuatorCommands.Lower)
            {
                _angle = Math.Max(-30.0, _angle - delta);
                if (_angle <= -30.0)
                {
                    _currentMotion = null;
                    _moveTimestamp = null;
                    TryTransition(ActuatorStates.Completed);
                    // Sleep for 500ms, then transition to Idle
                    System.Threading.Thread.Sleep(500);
                    TryTransition(ActuatorStates.Idle);
                }
            }
            else
            {
                // If not moving, clear motion/timestamp and go Idle
                _currentMotion = null;
                _moveTimestamp = null;
                TryTransition(ActuatorStates.Idle);
            }

            // Reset timestamp so subsequent telemetry increments from now
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
