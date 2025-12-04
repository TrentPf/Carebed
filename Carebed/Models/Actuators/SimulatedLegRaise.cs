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
        private const double _cmPerSecond = 16.67;
        private Task? _movementTask;
        private CancellationTokenSource? _movementCts;

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
                    // Simulate movement duration (customize as needed)
                    await Task.Delay(TimeSpan.FromSeconds(3), _movementCts.Token);

                    TryTransition(ActuatorStates.Completed);

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

        private void UpdateExtension()
        {
            if (!_moveTimestamp.HasValue || !_currentMotion.HasValue)
                return;

            var seconds = (DateTime.UtcNow - _moveTimestamp.Value).TotalSeconds;
            var delta = seconds * _cmPerSecond;

            if (_currentMotion == ActuatorCommands.Raise)
            {
                _extension = Math.Min(50.0, _extension + delta);
                if (_extension >= 50.0)
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
                _extension = Math.Max(0.0, _extension - delta);
                if (_extension <= 0.0)
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
