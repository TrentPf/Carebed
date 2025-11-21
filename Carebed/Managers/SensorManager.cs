using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Models.Actuators;
using Carebed.Models.Sensors;

namespace Carebed.Managers
{
    /// <summary>
    /// Top-level manager that polls configured sensors on a single timer and
    /// publishes their readings to the application's <see cref="IEventBus"/>.
    /// </summary>
    internal class SensorManager : IManager
    {

        #region Fields and Properties

        private readonly IEventBus _eventBus;
        private readonly List<ISensor> _sensors;
        private readonly System.Timers.Timer _timer;
        private int _isPolling;

        /// <summary>
        /// A dictionary for mapping sensors to their state change handlers.
        /// </summary>
        private readonly Dictionary<ISensor, Action<SensorStates>> _stateChangedHandlers = new();

        #endregion


        /// <summary>
        /// Initializes a new instance of the <see cref="SensorManager"/> class, which manages a collection of sensors
        /// and periodically polls their data, publishing the results to an event bus.
        /// </summary>
        /// <remarks>The <see cref="SensorManager"/> class uses a timer to periodically poll the provided
        /// sensors at the specified interval. The polling results are published to the provided event bus. If a
        /// <paramref name="synchronizingObject"/> is provided, the timer ensures that event-handler calls are
        /// marshaled to the thread that owns the synchronizing object.</remarks>
        /// <param name="eventBus">The event bus used to publish sensor data. Cannot be <see langword="null"/>.</param>
        /// <param name="sensorList">The collection of sensors to be managed and polled. Cannot be <see langword="null"/> or empty.</param>
        /// <param name="intervalMilliseconds">The interval, in milliseconds, at which the sensors are polled. Defaults to 1000 milliseconds.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventBus"/> is <see langword="null"/> or if <paramref name="sensorList"/> is <see
        /// langword="null"/>.</exception>
        public SensorManager(IEventBus eventBus, List<ISensor> sensorList, double intervalMilliseconds = 1000)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _sensors = sensorList ?? throw new ArgumentNullException(nameof(sensorList));

            // Create a timer to poll sensors at the specified interval
            _timer = new System.Timers.Timer(intervalMilliseconds) { AutoReset = true };

            // Attach the polling method to the timer's Elapsed event (subscribe to event)
            // Will fire once per interval
            _timer.Elapsed += (s, e) => _ = PollOnceAsync();

            // Future improvement: Check for duplicate actuator IDs and handle accordingly.
            foreach (var sensor in sensorList)
            {
                Action<SensorStates> handler = state => HandleStateChanged(sensor, state);
                _stateChangedHandlers[sensor] = handler;
                sensor.OnStateChanged += handler;
                _sensors.Add(sensor);            }

            // Emit initial inventory message
            EmitSensorInventoryMessage();
        }

        private void HandleStateChanged(ISensor sensor, SensorStates state)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Starts the timer and the sensor(s).
        /// </summary>
        public void Start()
        {            
            foreach (var s in _sensors) s.Start();
            StartTimer();
        }

        /// <summary>
        /// Stops the timer and the sensor(s).
        /// </summary>
        public void Stop()
        {
            StopTimer();
            foreach (var s in _sensors) s.Stop();
        }

        /// <summary>
        /// Start the internal polling timer.
        /// </summary>
        public void StartTimer()
        {
            _timer.Start();
        }

        /// <summary>
        /// Stop the internal polling timer.
        /// </summary>
        public void StopTimer()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Start a specific sensor by its ID.
        /// </summary>
        /// <param name="sensorId"></param>
        public void StartSensor(string sensorId)
        {
            var sensor = _sensors.Find(s => s.SensorID == sensorId);
            sensor?.Start();
        }

        /// <summary>
        /// Stop a specific sensor by its ID.
        /// </summary>
        /// <param name="sensorId"></param>
        public void StopSensor(string sensorId)
        {
            var sensor = _sensors.Find(s => s.SensorID == sensorId);
            sensor?.Stop();
        }

        /// <summary>
        /// Allows adjustment of the polling interval at runtime.
        /// </summary>
        /// <param name="intervalSeconds"></param>
        /// <returns></returns>
        public bool AdjustPollingRate(double intervalSeconds)
        {
            if (intervalSeconds <= 0) return false;
            if (intervalSeconds >= 60) return false;

            _timer.Interval = intervalSeconds * 1000;
            return true;
        }

        /// <summary>
        /// Executes a single polling operation to read data from all registered sensors and publish the data
        /// asynchronously to the event bus.
        /// </summary>
        /// <remarks>This method ensures that only one polling operation is executed at a time by using a
        /// thread-safe mechanism to prevent overlapping polls. Each sensor's data is read and published as a separate
        /// task. If a sensor fails to read data, the error is logged, and the polling operation continues for other
        /// sensors. If any publishing task fails, the exception is logged, but the method completes
        /// execution.</remarks>
        /// <returns></returns>
        private async Task PollOnceAsync()
        {
            // prevent overlapping polls
            if (Interlocked.Exchange(ref _isPolling, 1) == 1) return;

            try
            {
                var publishTasks = new List<Task>(_sensors.Count);
                foreach (var sensor in _sensors)
                {
                    try
                    {
                        var payload = sensor.ReadData();
                        SensorTelemetryMessage newSensorTelemetryMessage = new SensorTelemetryMessage
                        {
                            SensorID = sensor.SensorID,
                            TypeOfSensor = payload.SensorType,
                            Data = payload,
                            CreatedAt = DateTime.UtcNow,
                            CorrelationId = Guid.NewGuid(),
                            Metadata = null,
                            IsCritical = false
                        };
                        var envelope = new MessageEnvelope<SensorTelemetryMessage>(newSensorTelemetryMessage, MessageOrigins.SensorManager, MessageTypes.SensorData);
                        publishTasks.Add(_eventBus.PublishAsync(envelope));
                    }
                    catch (Exception exSensor)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sensor {sensor?.SensorID ?? "<unknown>"} read failed: {exSensor}");
                    }
                }

                if (publishTasks.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(publishTasks).ConfigureAwait(false);
                    }
                    catch (Exception exPub)
                    {
                        System.Diagnostics.Debug.WriteLine($"Publishing tasks failed: {exPub}");
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isPolling, 0);
            }
        }

        /// <summary>
        /// Releases all resources used by the current instance of the class.
        /// </summary>
        /// <remarks>This method stops any ongoing operations, disposes of the internal timer, and
        /// releases resources  held by all associated sensors. After calling this method, the instance should not be
        /// used.</remarks>
        public void Dispose()
        {
            Stop();
            _timer?.Dispose();

            // Iterate through the sensors and unsubscribe from their state change events
            foreach (var item in _stateChangedHandlers)
                item.Key.OnStateChanged -= item.Value;
            _stateChangedHandlers.Clear();
        }

        public void EmitSensorInventoryMessage()
        {
            var metadata = new Dictionary<string, string>();

            foreach (var sensor in _sensors)
            {
                metadata[sensor.SensorID] = sensor.SensorType.ToString();
            }

            var inventoryMessage = new SensorInventoryMessage
            {
                SensorID = "SensorManager",
                TypeOfSensor = SensorTypes.Manager,
                Metadata = metadata
            };
            var envelope = new MessageEnvelope<SensorInventoryMessage>(
                inventoryMessage,
                MessageOrigins.SensorManager,
                MessageTypes.SensorInventory
            );

            // Fire-and-forget the async publish; BasicEventBus executes handlers on thread-pool.
            _ = _eventBus.PublishAsync(envelope);

        }
    }
}




