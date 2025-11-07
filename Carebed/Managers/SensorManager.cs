using System.ComponentModel;
using Carebed.Domain.Sensors;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Infrastructure.Enums;
using Carebed.Managers;

namespace Carebed.Modules
{
    /// <summary>
    /// Top-level manager that polls configured sensors on a single timer and
    /// publishes their readings to the application's <see cref="IEventBus"/>.
    /// </summary>
    internal class SensorManager : IManager
    {
        private readonly IEventBus _eventBus;
        private readonly List<ISensor> _sensors;
        private readonly System.Timers.Timer _timer;
        private int _isPolling;


        /// <summary>
        /// Initializes a new instance of the <see cref="SensorManager"/> class, which manages a collection of sensors
        /// and periodically polls their data, publishing the results to an event bus.
        /// </summary>
        /// <remarks>The <see cref="SensorManager"/> class uses a timer to periodically poll the provided
        /// sensors at the specified interval. The polling results are published to the provided event bus. If a
        /// <paramref name="synchronizingObject"/> is provided, the timer ensures that event-handler calls are
        /// marshaled to the thread that owns the synchronizing object.</remarks>
        /// <param name="eventBus">The event bus used to publish sensor data. Cannot be <see langword="null"/>.</param>
        /// <param name="sensors">The collection of sensors to be managed and polled. Cannot be <see langword="null"/> or empty.</param>
        /// <param name="intervalMilliseconds">The interval, in milliseconds, at which the sensors are polled. Defaults to 1000 milliseconds.</param>
        /// <param name="synchronizingObject">An optional object used to marshal event-handler calls that are triggered by the polling timer. If <see
        /// langword="null"/>, event-handler calls are not marshaled to a specific thread.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="eventBus"/> is <see langword="null"/> or if <paramref name="sensors"/> is <see
        /// langword="null"/>.</exception>
        public SensorManager(IEventBus eventBus, IEnumerable<ISensor> sensors, double intervalMilliseconds = 1000, ISynchronizeInvoke? synchronizingObject = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _sensors = sensors?.ToList() ?? throw new ArgumentNullException(nameof(sensors));

            _timer = new System.Timers.Timer(intervalMilliseconds) { AutoReset = true };
            if (synchronizingObject is not null)
                _timer.SynchronizingObject = synchronizingObject;

            _timer.Elapsed += (s, e) => _ = PollOnceAsync();
        }

        /// <summary>
        /// Starts the timer(s) for the sensor(s).
        /// </summary>
        public void Start()
        {
            foreach (var s in _sensors) s.Start();
            //_eventBus.Initialize(); // This method is actually run in SystemInitializer and only needs to be run once - so I am commenting it out here. - M.S.
            _timer.Start();
        }

        /// <summary>
        /// Stops the timer(s) for the sensor(s).
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            foreach (var s in _sensors) s.Stop();
            //_eventBus.Shutdown(); // This method is actually run in SystemInitializer and only needs to be run once - so I am commenting it out here.
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
                        var envelope = new MessageEnvelope<SensorData>(payload, MessageOriginEnum.SensorManager, MessageTypeEnum.SensorData);
                        publishTasks.Add(_eventBus.PublishAsync(envelope));
                    }
                    catch (Exception exSensor)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sensor {sensor?.Source ?? "<unknown>"} read failed: {exSensor}");
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
            foreach (var s in _sensors) s.Dispose();
        }
    }
}
