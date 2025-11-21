using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Models.Actuators;

namespace Carebed.Models.Sensors
{   
    /// <summary>
    /// Base implementation for sensors. Provides Source property and basic lifecycle stubs.
    /// Concrete sensors implement <see cref="ReadData"/>.
    /// </summary>
    public abstract class AbstractSensor : ISensor
    {

        #region Fields and Properties

        /// <summary>
        /// Represents the minimum value the sensor can read.
        /// </summary>
        protected readonly double _min;

        /// <summary>
        /// Represents the maximum value the sensor can read.
        /// </summary>
        protected readonly double _max;

        /// <summary>
        /// Represents the critical threshold for the sensor. Sensor readings beyond this threshold indicate a critical condition.
        /// </summary>
        protected readonly double _criticalThreshold;

        /// <summary>
        /// The unique identifier for this sensor instance (e.g., "tempSensor1, "heartRateA").
        /// </summary>
        public string SensorID { get; init; }

        /// <summary>
        /// Gets the current state of the actuator.
        /// </summary>
        public SensorStates CurrentState => _stateMachine.Current;

        public SensorTypes SensorType { get; init; }

        /// <summary>
        /// A state machine to manage the actuator's states and transitions.
        /// </summary>
        protected readonly StateMachine<SensorStates> _stateMachine;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Constructor for the AbstractSensor class.
        /// </summary>
        protected AbstractSensor(string sensorID, SensorTypes sensorType, double min, double max, double criticalThreshold)
        {
            SensorID = sensorID ?? throw new ArgumentNullException(nameof(sensorID));
            SensorType = sensorType;
            _min = min;
            _max = max;
            _criticalThreshold = criticalThreshold; 
            _stateMachine = new StateMachine<SensorStates>(SensorStates.Uninitialized, GetTransitionMap());
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Event triggered when the actuator transitions to a new state.
        /// </summary>
        public event Action<SensorStates>? OnStateChanged;

        #endregion

        #region Methods

        /// <summary>
        /// Transitions the sensor to the Running state.
        /// </summary>
        public virtual void Start()
        {
            // Start internal timer, open hardware connection, etc.
            _stateMachine.TryTransition(SensorStates.Running);
        }

        /// <summary>
        /// Transitions the sensor to the Stopped state.
        /// </summary>
        public virtual void Stop()
        {
            // Stop timer, close connection, etc.
            _stateMachine.TryTransition(SensorStates.Stopped);
        }

        /// <summary>
        /// Resets the sensor to the Initialized state by transitioning through Uninitialized,
        /// Initialized, and then finally back into Running.
        /// </summary>
        public virtual void Reset()
        {
            _stateMachine.TryTransition(SensorStates.Uninitialized);
            _stateMachine.TryTransition(SensorStates.Initialized);
            _stateMachine.TryTransition(SensorStates.Running);
        }

        /// <summary>
        /// Reads data from a sensor, as long as it is in the Running state.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public SensorData ReadData()
        {
            if (CurrentState != SensorStates.Running)
                throw new InvalidOperationException($"Cannot read data unless sensor is in Running state. Current state: {CurrentState}");

            return ReadDataActual();
        }

        /// <summary>
        /// This is the actual ReadData implementation that derived classes must implement.
        /// </summary>
        /// <returns></returns>
        public abstract SensorData ReadDataActual();

        /// <summary>
        /// Gets the transition map for the sensor's states. Can be overridden by derived classes to customize state transitions.
        /// </summary>
        /// <returns></returns>
        protected virtual Dictionary<SensorStates, SensorStates[]> GetTransitionMap()
        {
            return new Dictionary<SensorStates, SensorStates[]>
            {
                { SensorStates.Uninitialized, new[] { SensorStates.Initialized, SensorStates.Error } },
                { SensorStates.Initialized, new[] { SensorStates.Running, SensorStates.Error } },
                { SensorStates.Running, new[] { SensorStates.Initialized, SensorStates.Stopped, SensorStates.Error } },
                { SensorStates.Stopped, new[] { SensorStates.Running, SensorStates.Calibrating, SensorStates.Error } },
                { SensorStates.Calibrating, new[] { SensorStates.Initialized, SensorStates.Error } },
                { SensorStates.Error, new[] { SensorStates.Uninitialized, SensorStates.Disconnected } },
                { SensorStates.Disconnected, new[] { SensorStates.Uninitialized } }
            };
        }

        /// <summary>
        /// Helper for building metadata dictionaries.
        /// </summary>
        protected static IReadOnlyDictionary<string, string> BuildMetadata(params (string Key, string Value)[] pairs)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        #endregion
    }
}