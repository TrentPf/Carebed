
using System.ComponentModel;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed
{
    /// <summary>
    /// WinForms-friendly sensor device that polls periodically and publishes
    /// strongly-typed MessageEnvelope{SensorData} to an IEventBus.
    /// Minimal, SCADA/HMI-oriented: 1s poll, simple critical threshold and metadata.
    /// </summary>
    //public sealed class SensorDevice : IDisposable
    //{
    //    //private readonly IEventBus _eventBus;
    //    //private readonly System.Timers.Timer _timer;
    //    //private readonly string _source;
    //    //private readonly Random _rng = new();
    //    //private readonly object _rngLock = new();
    //    //private readonly double _criticalThreshold;

    //    //public SensorDevice(IEventBus eventBus, string source, double intervalMilliseconds = 1000, ISynchronizeInvoke? synchronizingObject = null, double criticalThreshold = 45.0)
    //    //{
    //    //    _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    //    //    _source = source ?? throw new ArgumentNullException(nameof(source));
    //    //    _criticalThreshold = criticalThreshold;

    //    //    _timer = new System.Timers.Timer(intervalMilliseconds)
    //    //    {
    //    //        AutoReset = true
    //    //    };

    //    //    if (synchronizingObject != null)
    //    //        _timer.SynchronizingObject = synchronizingObject; // marshal Elapsed to UI thread if requested

    //    //    _timer.Elapsed += Timer_Elapsed;
    //    //}

    //    //public void Start() => _timer.Start();
    //    //public void Stop() => _timer.Stop();

    //    //private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    //    //{
    //    //    try
    //    //    {
    //    //        double value;
    //    //        lock (_rngLock)
    //    //        {
    //    //            value = _rng.NextDouble() * 50.0; // 0..50 °C
    //    //        }

    //    //        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    //    //        {
    //    //            ["Unit"] = "°C",
    //    //            ["Source"] = _source
    //    //        };

    //    //        var payload = new SensorData(
    //    //            Value: value,
    //    //            Source: _source,
    //    //            IsCritical: value >= _criticalThreshold,
    //    //            Metadata: metadata
    //    //        );

    //    //        var envelope = new MessageEnvelope<SensorData>(payload, MessageOrigins.SensorManager, MessageTypes.SensorData);

    //    //        // Fire-and-forget the async publish; BasicEventBus executes handlers on thread-pool.
    //    //        _ = _eventBus.PublishAsync(envelope);
    //    //    }
    //    //    catch (Exception ex)
    //    //    {
    //    //        // In a real SCADA/HMI system you'd log this to the diagnostics/error subsystem.
    //    //        System.Diagnostics.Debug.WriteLine($"SensorDevice publish error: {ex}");
    //    //    }
    //    //}

    //    //public void Dispose()
    //    //{
    //    //    _timer?.Stop();
    //    //    _timer?.Dispose();
    //    //}
    //}
}