using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.SensorMessages; // IManager
using Carebed.Infrastructure.MessageEnvelope;


/*
 * Refactored 2025-11-07 by Matthew Schat; Used copilot to analyse and came up with some adjustments.
 * Following logic for the changes:
 * Why this shape
    •	IManager is intentionally small and synchronous to match existing code.
    •	Moving start/stop out of constructors:
    •	avoids surprising background work at creation time,
    •	makes ownership and disposal deterministic from the composition root (SystemInitializer / Program.Main).
    •	Using Random.Shared inside SimulatedSensor avoids seed-collision issues when creating many Random instances quickly.
 * Next steps (optional)
    •	If managers might need async startup, consider Task StartAsync() / Task StopAsync() instead of the synchronous methods.
    •	Consider adding bool IsRunning { get; } to the interface if call-sites need to check state.
    •	Update SystemInitializer / Program to start and stop managers via the IManager contract.
 * 
 */

namespace Carebed.Managers
{
    public class SimulatedSensorManager : IManager
    {
        private enum SimulatedSensorNames{
            EEGSensor,
            BloodO2Sensor,
            TemperatureSensor,
            HeartRateSensor,
            BloodPressureSensor,
            RespirationRateSensor,
            GlucoseSensor
        }
        private readonly IEventBus _eventBus;
        private readonly List<SimulatedSensor> _sensors = new();
        private CancellationTokenSource? _cts;
        private Task? _task;
        private readonly int _intervalMs;
        private readonly object _lock = new();

        public SimulatedSensorManager(IEventBus eventBus, int sensorCount = 5, int intervalMs = 1000)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _intervalMs = Math.Max(100, intervalMs);

            var rnd = new Random();
            var sensorNames = Enum.GetValues(typeof(SimulatedSensorNames)).Cast<SimulatedSensorNames>().ToList();
            for (int i = 0; i < sensorCount; i++)
            {
                var baseline = 20.0 + rnd.NextDouble() * 80.0; // 20..100
                var name = i < sensorNames.Count
                    ? sensorNames[i].ToString()
                    : $"sensor-{i + 1}";
                _sensors.Add(new SimulatedSensor(name, baseline));
            }
        }

        /// <summary>
        /// Starts the simulated sensor manager.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                // Check if already running
                if (_task != null && !_task.IsCompleted) return;

                // Setup a cancellation token
                _cts = new CancellationTokenSource();

                // Start the background task
                _task = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
            }
        }

        /// <summary>
        /// Stops the ongoing operation, if one is currently running.
        /// </summary>
        /// <remarks>This method cancels the operation by signaling a cancellation token and waits for the
        /// associated task  to complete for up to 500 milliseconds. If no operation is running, the method does
        /// nothing.</remarks>
        public void Stop()
        {
            lock (_lock)
            {
                if (_cts == null) return;
                try
                {
                    _cts.Cancel();
                    _task?.Wait(500);
                }
                catch { }
                finally
                {
                    _cts.Dispose();
                    _cts = null;
                    _task = null;
                }
            }
        }

        /// <summary>
        /// Runs the sensor simulation loop.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var s in _sensors)
                    {
                        var value = s.NextValue();

                        SensorData payload = new SensorData
                        {
                            Value = value,
                            Source = "SimulatedTemperatureSensor1",
                            SensorType = SensorTypes.Temperature,
                            IsCritical = false,
                            CreatedAt = DateTime.UtcNow,
                            CorrelationId = Guid.NewGuid(),
                            Metadata = new Dictionary<string, string>
                            {
                                { "Unit", "units" },
                                { "Sensor", "SimulatedTemperatureSensor1" }
                            }
                        };
                        SensorTelemetryMessage newSensorTelemetryMessage = new SensorTelemetryMessage
                        {
                            SensorID = payload.Source,
                            TypeOfSensor = payload.SensorType,
                            Data = payload,
                            CreatedAt = DateTime.UtcNow,
                            CorrelationId = Guid.NewGuid(),
                            Metadata = null,
                            IsCritical = false
                        };
                        var envelope = new MessageEnvelope<SensorTelemetryMessage>(newSensorTelemetryMessage, MessageOrigins.SensorManager, MessageTypes.SensorData);
                        await _eventBus.PublishAsync(envelope);
                    }

                    await Task.Delay(_intervalMs, token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SimulatedSensorManager loop failed: {ex}");
            }
        }

        /// <summary>
        /// Disposes the resources used by the simulated sensor manager.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Represents a simulated sensor.
        /// </summary>
        private class SimulatedSensor
        {
            private readonly Random _rnd = Random.Shared;
            public string Name { get; }
            private double _current;

            public SimulatedSensor(string name, double baseline)
            {
                Name = name;
                _current = baseline;
            }

            public double NextValue()
            {
                var delta = (_rnd.NextDouble() - 0.5) * 4.0; // +/-2.0
                _current = Math.Max(0.0, _current + delta);
                if (_rnd.NextDouble() < 0.02)
                {
                    _current += (_rnd.NextDouble() * 40.0) - 20.0;
                    if (_current < 0) _current = 0;
                }

                return Math.Round(_current, 2);
            }
        }
    }
}
