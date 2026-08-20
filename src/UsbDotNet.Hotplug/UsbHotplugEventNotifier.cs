using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <summary>
/// Raises classic .NET events for an <see cref="IUsbHotplugMonitor"/> subscription, for code that
/// would rather attach handlers than write an <c>await foreach</c>.
/// <para>
/// Construct it, attach handlers, then start it — in that order. <see cref="Start"/> runs the
/// subscription in the background until <see cref="DisposeAsync"/> stops it, while
/// <see cref="RunAsync"/> hands the same subscription back as a task the caller owns. Use one or the
/// other; a notifier serves a single subscription.
/// </para>
/// <para>
/// The monitor is not owned. Disposing the notifier ends its own subscription and leaves the monitor
/// running for its other subscribers.
/// </para>
/// </summary>
public sealed class UsbHotplugEventNotifier : IAsyncDisposable
{
    private readonly IUsbHotplugMonitor _monitor;
    private readonly IUsbDeviceFilter? _filter;
    private readonly ILogger<UsbHotplugEventNotifier> _logger;

    // Guards the handover between Start and DisposeAsync; the two fields below it are written once.
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _run;

    private int _running;

    // Read by the subscription loop, written by DisposeAsync from whatever thread disposes.
    private volatile bool _disposed;

    /// <summary>Raised when a matching device is connected, or was already connected.</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceConnected;

    /// <summary>Raised when a matching device is disconnected.</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Creates a notifier over <paramref name="monitor"/>. Nothing is subscribed yet; attach
    /// handlers and then call <see cref="Start"/> or <see cref="RunAsync"/>.
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
    /// Subscribes and raises <see cref="DeviceConnected"/> / <see cref="DeviceDisconnected"/> in the
    /// background until <see cref="DisposeAsync"/> or a cancelled
    /// <paramref name="cancellationToken"/> ends it.
    /// <para>
    /// <strong>Attach your handlers before calling this.</strong> Devices already connected are
    /// raised as <see cref="DeviceConnected"/> before this returns, so a handler attached afterwards
    /// misses them.
    /// </para>
    /// <para>
    /// The exceptions documented on <see cref="IUsbHotplugMonitor.Subscribe"/> surface from here,
    /// since a monitor that cannot be subscribed says so on the first read and that read happens
    /// inside this call. A failure that only shows up later surfaces from
    /// <see cref="DisposeAsync"/> instead. Handler exceptions never propagate: each handler is
    /// invoked individually and a thrower is logged at warning level, so one bad handler cannot deny
    /// the event to the others.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">
    /// Ends the subscription when cancelled, for callers who would rather stop it that way than by
    /// disposing.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this notifier has already been started or run. The monitor's stream is reusable,
    /// but a second subscription over the same handlers would raise every event twice.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this notifier has been disposed.</exception>
    public void Start(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Claim();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Invoked on this thread rather than through Task.Run: the subscription delivers the
            // already-connected devices before its first incomplete await, so a handler attached
            // before this call cannot miss them.
            var run = SubscribeAsync(_cts.Token);
            if (run.IsFaulted)
            {
                // The monitor rejected the subscription during the prologue above. Nothing is left
                // behind, so disposal has neither a token source to release nor a failure to report
                // a second time.
                _cts.Dispose();
                _cts = null;
                ExceptionDispatchInfo.Capture(run.Exception!.InnerException!).Throw();
            }
            _run = run;
        }
    }

    /// <summary>
    /// Subscribes and raises <see cref="DeviceConnected"/> / <see cref="DeviceDisconnected"/> until
    /// the subscription ends. Devices already connected when this is called are raised as
    /// <see cref="DeviceConnected"/> first, then live events follow.
    /// <para>
    /// <strong>Attach your handlers before calling this.</strong> The initial burst is delivered
    /// inside this method's synchronous prologue, so a handler attached after the call can miss it.
    /// </para>
    /// <para>
    /// A live subscription has no natural end, so this returns only once something stops it.
    /// Cancelling <paramref name="cancellationToken"/> throws an
    /// <see cref="OperationCanceledException"/> carrying that token; disposing the monitor or the
    /// underlying <see cref="IUsb"/> throws one carrying no token, with a message saying which;
    /// disposing the notifier returns normally. The exceptions documented on
    /// <see cref="IUsbHotplugMonitor.Subscribe"/> surface from here too, since this is what starts
    /// the subscription. Handler exceptions do <em>not</em> propagate: each handler is invoked
    /// individually and a thrower is logged at warning level, so one bad handler cannot deny the
    /// event to the others.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this notifier has already been started or run. The monitor's stream is reusable,
    /// but a second subscription over the same handlers would raise every event twice.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this notifier has been disposed.</exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Claim();
        await SubscribeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the subscription and waits for it to unwind, so nothing is dispatched after this is
    /// called and no handler is still running once it returns. Idempotent, and harmless on a
    /// notifier that was never started. The monitor is left running.
    /// <para>
    /// Cancellation is expected and stays here — the caller's own token, and the untokened
    /// cancellation that means the monitor or the underlying <see cref="IUsb"/> went away, both end
    /// the subscription, which is what disposal asked for. Any other failure is rethrown, since
    /// nothing else is left to report that the subscription died.
    /// </para>
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Task? run;
        CancellationTokenSource? cts;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            // Set before cancelling: this, not the token, is what stops delivery mid-iteration.
            _disposed = true;
            run = _run;
            cts = _cts;
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }
        try
        {
            if (run is not null)
            {
                await run.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ours, or the monitor going away underneath. Either way the subscription is over.
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private void Claim()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UsbHotplugEventNotifier));
        }
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            throw new InvalidOperationException(
                "This notifier has already been run. Construct another one for a second "
                    + "subscription."
            );
        }
    }

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        await foreach (
            var e in _monitor.Subscribe(_filter, cancellationToken).ConfigureAwait(false)
        )
        {
            // Disposal stops delivery at once. Cancellation alone would not: an event already read
            // from the stream would still reach a handler while the loop unwinds.
            if (_disposed)
            {
                break;
            }
            var handler =
                e.Type == UsbHotplugEventType.Connected ? DeviceConnected : DeviceDisconnected;
            EventDispatch.RaiseSafely(
                handler,
                _logger,
                this,
                new UsbHotplugEventArgs(e.Descriptor),
                e.Descriptor.DeviceKey
            );
        }
    }
}
