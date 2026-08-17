using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <summary>
/// Adapts the channel-based <see cref="IUsbHotplugMonitor"/> subscription to classic .NET events.
/// <see cref="Start"/> subscribes with the given filter and pumps the subscription channel on a
/// background task, raising <see cref="DeviceConnected"/> and <see cref="DeviceDisconnected"/>
/// events.
/// <para>
/// Usage: construct, attach handlers, then call <see cref="Start"/>. The subscription is created
/// in <see cref="Start"/> and the initial snapshot of connected devices delivered to the handler.
/// </para>
/// </summary>
public sealed class UsbHotplugEventNotifier : IDisposable
{
    private readonly object _lock = new();
    private readonly IUsbHotplugMonitor _monitor;
    private readonly IUsbDeviceFilter? _filter;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<UsbHotplugEventNotifier> _logger;
    private IUsbHotplugSubscription? _subscription;
    private Task? _pump;
    private bool _disposed;

    // Managed id of the thread currently invoking event handlers, or 0 when none is in flight.
    // Used by Dispose to detect when it is being called from within a handler (i.e. on the pump
    // thread), where a synchronous Wait() on the pump would deadlock the thread against itself.
    private volatile int _dispatchThreadId;

    /// <summary>Raised when a matching device is connected (or was already connected).</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceConnected;

    /// <summary>Raised when a matching device is disconnected.</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Creates a notifier over <paramref name="monitor"/>. No subscription is created yet;
    /// call <see cref="Start"/> after attaching handlers to subscribe and begin raising events.
    /// </summary>
    /// <param name="monitor">The monitor to subscribe to.</param>
    /// <param name="filter">The filter to apply, or null for all devices.</param>
    /// <param name="loggerFactory">Optional logger factory. If null, logging is disabled.</param>
    public UsbHotplugEventNotifier(
        IUsbHotplugMonitor monitor,
        IUsbDeviceFilter? filter = null,
        ILoggerFactory? loggerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        _monitor = monitor;
        _filter = filter;
        _logger = loggerFactory is null
            ? NullLogger<UsbHotplugEventNotifier>.Instance
            : loggerFactory.CreateLogger<UsbHotplugEventNotifier>();
    }

    /// <summary>
    /// Subscribes to the monitor and begins raising events on a background task. Attach
    /// <see cref="DeviceConnected"/> and <see cref="DeviceDisconnected"/> handlers before
    /// calling this so the initial snapshot of connected devices is delivered.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the notifier is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the monitor cannot create a subscription; see
    /// <see cref="IUsbHotplugMonitor.Subscribe"/>. The notifier remains unstarted.
    /// </exception>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UsbHotplugEventNotifier));
            }
            // A non-null _pump marks the notifier as started; it is only assigned below.
            if (_pump is not null)
            {
                return;
            }
            // Subscribe before creating the pump so a throwing Subscribe leaves the notifier
            // unstarted. The monitor replays already connected devices into the subscription
            // channel at subscribe time; the pump below delivers them to the attached handlers.
            var subscription = _monitor.Subscribe(_filter);
            _subscription = subscription;
            // Read the token under _lock, and before scheduling: a concurrent Dispose cannot
            // dispose _cts between the disposed check above and this read, and the deferred
            // pump task never touches _cts itself.
            var token = _cts.Token;
            _pump = Task.Run(() => PumpAsync(subscription, token));
        }
    }

    private async Task PumpAsync(IUsbHotplugSubscription subscription, CancellationToken token)
    {
        try
        {
            await foreach (var e in subscription.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                var handler =
                    e.Type == UsbHotplugEventType.Connected ? DeviceConnected : DeviceDisconnected;
                if (handler is null)
                {
                    continue;
                }
                // Record the dispatching thread so Dispose can tell whether it is being called
                // from within a handler (on this thread) and must not wait on the pump.
                _dispatchThreadId = Environment.CurrentManagedThreadId;
                try
                {
                    EventDispatch.RaiseSafely(
                        handler,
                        _logger,
                        this,
                        new UsbHotplugEventArgs(e.Descriptor),
                        e.Descriptor.DeviceKey
                    );
                }
                finally
                {
                    _dispatchThreadId = 0;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when this notifier (own token), the owning monitor, or the underlying
            // IUsb is disposed; the latter two cancel the subscription channel.
        }
    }

    /// <summary>Stops pumping events and disposes the underlying subscription, if any.</summary>
    public void Dispose()
    {
        Task? pump;
        IUsbHotplugSubscription? subscription;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            pump = _pump;
            subscription = _subscription;
        }
        // Cancel and wait outside _lock; only state transitions are serialized by it.
        _cts.Cancel();
        subscription?.Dispose();
        if (pump is null)
        {
            // Start can no longer create a pump (_disposed is set), so _cts is safe to dispose.
            _cts.Dispose();
            return;
        }
        // If Dispose is called from within an event handler we are running on the pump thread, so
        // waiting for the pump to finish would deadlock the thread against itself. In that case
        // skip the wait: the pump unwinds on its own once the handler returns and observes the
        // cancellation. Defer disposing the CTS until the pump has actually completed, otherwise
        // we would dispose a token the still-unwinding pump is about to read.
        if (Environment.CurrentManagedThreadId == _dispatchThreadId)
        {
            _ = pump.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                _cts,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            return;
        }
        try
        {
            // Synchronous wait for the background pump to drain. Safe here because we are not on
            // the pump thread; it exits promptly once cancelled, unless a handler blocks forever.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            pump.Wait();
#pragma warning restore VSTHRD002
        }
        catch (AggregateException)
        {
            // Ignore faults from the pump task on shutdown.
        }
        _cts.Dispose();
    }
}
