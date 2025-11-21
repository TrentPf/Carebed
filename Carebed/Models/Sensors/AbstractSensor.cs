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

        /// <summary>
        /// A state machine to manage the actuator's states and transitions.
        /// </summary>
        protected readonly StateMachine<SensorStates> _stateMachine;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Constructor for the AbstractSensor class.
        /// </summary>
        protected AbstractSensor(string sensorID, double min, double max, double criticalThreshold)
        {
            SensorID = sensorID ?? throw new ArgumentNullException(nameof(sensorID));
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
        public event Action<ActuatorStates>? OnStateChanged;

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
        /// Return a single snapshot of sensor data. Implementations should include unit metadata.
        /// </summary>
        public abstract SensorData ReadData();  

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