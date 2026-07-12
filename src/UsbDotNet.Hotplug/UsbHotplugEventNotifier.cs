using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <summary>
/// Adapts the channel-based <see cref="IUsbHotplugMonitor"/> subscription to classic .NET events.
/// It subscribes with the given filter and, once <see cref="Start"/> is called, pumps the channel
/// on a background task, raising <see cref="DeviceConnected"/> and <see cref="DeviceDisconnected"/>
/// events.
/// <para>
/// Usage: construct, attach handlers, then call <see cref="Start"/>. The subscription (including
/// the initial snapshot of already connected devices) is captured at construction and buffered, so
/// no events are lost between construction and <see cref="Start"/>.
/// </para>
/// </summary>
public sealed class UsbHotplugEventNotifier : IDisposable
{
    private readonly object _lock = new();
    private readonly IUsbHotplugSubscription _subscription;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<UsbHotplugEventNotifier> _logger;
    private Task? _pump;
    private bool _started;
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
    /// Subscribes to <paramref name="monitor"/> with the given filter.
    /// Call <see cref="Start"/> after attaching handlers to begin raising events.
    /// </summary>
    /// <param name="monitor">The monitor to subscribe to.</param>
    /// <param name="filter">The filter to apply, or null for all devices.</param>
    /// <param name="loggerFactory">Optional logger factory. If null, logging is disabled.</param>
    public UsbHotplugEventNotifier(
        IUsbHotplugMonitor monitor,
        UsbDeviceFilter? filter = null,
        ILoggerFactory? loggerFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        _logger = loggerFactory is null
            ? NullLogger<UsbHotplugEventNotifier>.Instance
            : loggerFactory.CreateLogger<UsbHotplugEventNotifier>();
        // Subscribe now so the initial snapshot of connected devices is captured; events buffer in
        // the channel until Start() drains them to the (by then attached) handlers.
        _subscription = monitor.Subscribe(filter);
    }

    /// <summary>
    /// Begins raising events on a background task. Attach <see cref="DeviceConnected"/> and
    /// <see cref="DeviceDisconnected"/> handlers before calling this so the initial snapshot of
    /// connected devices is delivered to them.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the notifier is disposed.</exception>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UsbHotplugEventNotifier));
            }
            if (_started)
            {
                return;
            }
            _started = true;
            // Read the token under _lock, and before scheduling: a concurrent Dispose cannot
            // dispose _cts between the disposed check above and this read, and the deferred
            // pump task never touches _cts itself.
            var token = _cts.Token;
            _pump = Task.Run(() => PumpAsync(token));
        }
    }

    private async Task PumpAsync(CancellationToken token)
    {
        try
        {
            await foreach (var e in _subscription.Reader.ReadAllAsync(token).ConfigureAwait(false))
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
            // Expected when disposed.
        }
    }

    /// <summary>Stops pumping events and disposes the underlying subscription.</summary>
    public void Dispose()
    {
        Task? pump;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            pump = _pump;
        }
        // Cancel and wait outside _lock; only state transitions are serialized by it.
        _cts.Cancel();
        _subscription.Dispose();
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
