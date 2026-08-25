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
/// subscription in the background until <see cref="DisposeAsync"/> or <see cref="Dispose"/> stops
/// it, while <see cref="RunAsync"/> hands the same subscription back as a task the caller owns.
/// Use one or the other; a notifier serves a single subscription.
/// </para>
/// <para>
/// The monitor is not owned. Disposing the notifier ends its own subscription and leaves the
/// monitor running for its other subscribers.
/// </para>
/// </summary>
public sealed class UsbHotplugEventNotifier : IDisposable, IAsyncDisposable
{
    private readonly IUsbHotplugMonitor _monitor;
    private readonly IUsbDeviceFilter? _filter;
    private readonly ILogger<UsbHotplugEventNotifier> _logger;

    // Guards the started/disposed state and the handover between Start and DisposeAsync;
    // _cts and _run are written once.
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _run;
    private bool _running;

    // Read by the subscription loop, written by DisposeAsync from whatever thread disposes.
    private volatile bool _disposed;

    // The thread currently delivering an event, or 0. Set and cleared by the loop around each
    // dispatch with no await in between, so a thread that reads its own id here can only be inside
    // one of its handlers — the one case where disposal must not wait for the loop.
    private volatile int _dispatchThreadId;

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
        _logger =
            loggerFactory?.CreateLogger<UsbHotplugEventNotifier>()
            ?? NullLogger<UsbHotplugEventNotifier>.Instance;
    }

    /// <summary>
    /// Subscribes and raises <see cref="DeviceConnected"/> / <see cref="DeviceDisconnected"/> in
    /// the background until <see cref="DisposeAsync"/> or a cancelled
    /// <paramref name="ct"/> ends it.
    /// <para>
    /// <strong>Attach your handlers before calling this.</strong> Devices already connected are
    /// raised as <see cref="DeviceConnected"/> before this returns, so a handler attached
    /// afterwards misses them.
    /// </para>
    /// <para>
    /// The exceptions documented on <see cref="IUsbHotplugMonitor.Subscribe"/> surface from here,
    /// since a monitor that cannot be subscribed says so on the first read and that read happens
    /// inside this call. A failure that only shows up later surfaces from
    /// <see cref="DisposeAsync"/> instead. Handler exceptions never propagate: each handler is
    /// invoked individually and a thrower is logged at warning level, so one bad handler cannot
    /// deny the event to the others.
    /// </para>
    /// </summary>
    /// <param name="ct">
    /// Ends the subscription when cancelled, for callers who would rather stop it that way than by
    /// disposing.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this notifier has already been started or run. The monitor's stream is reusable,
    /// but a second subscription over the same handlers would raise every event twice.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this notifier has been disposed.
    /// </exception>
    public void Start(CancellationToken ct = default)
    {
        lock (_lock)
        {
            Claim();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Invoked on this thread rather than through Task.Run: the subscription delivers the
            // already-connected devices before its first incomplete await, so a handler attached
            // before this call cannot miss them.
            var run = SubscribeAsync(_cts.Token);
            if (_disposed)
            {
                // A handler disposed us during the prologue above, before _run existed for the
                // disposal to take with it. Finish its job here instead, exactly as a disposal
                // from a live event's handler would.
                _ = FinishDisposalAsync(run, _cts);
                _cts = null;
                return;
            }
            if (run.IsFaulted)
            {
                // The monitor rejected the subscription during the prologue above. Nothing is left
                // behind, so disposal has neither a token source to release nor a failure to report
                // a second time.
                _cts.Dispose();
                _cts = null;
                // Not GetAwaiter().GetResult(): the task is known-faulted, but VSTHRD002 forbids
                // the sync wait, so unwrap and rethrow with the original stack instead.
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
    /// Cancelling <paramref name="ct"/> throws an
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
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this notifier has been disposed.
    /// </exception>
    public async Task RunAsync(CancellationToken ct)
    {
        Claim();
        await SubscribeAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous counterpart to <see cref="DisposeAsync"/>: stops the subscription and blocks
    /// until it unwinds, with the same guarantees — nothing is dispatched after this is called, no
    /// handler is still running once it returns, cancellation is swallowed and any other
    /// subscription failure is rethrown. Called from inside a handler it does not block on the
    /// loop, with the same softened guarantees <see cref="DisposeAsync"/> documents for that case.
    /// </summary>
    public void Dispose() =>
        // VSTHRD002: sync-over-async is this method's contract, and the wait is deadlock-free —
        // the subscription runs ConfigureAwait(false) throughout, so it unwinds on the thread pool
        // and never needs the thread parked here, and a disposal from inside a handler skips the
        // wait on the loop entirely.
#pragma warning disable VSTHRD002
        DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

    /// <summary>
    /// Stops the subscription and waits for it to unwind, so nothing is dispatched after this is
    /// called and no handler is still running once it returns. Idempotent, and harmless on a
    /// notifier that was never started. The monitor is left running.
    /// <para>
    /// Cancellation is expected and stays here — the caller's own token, and the untokened
    /// cancellation that means the monitor or the underlying <see cref="IUsb"/> went away, both
    /// end the subscription, which is what disposal asked for. Any other failure is rethrown, since
    /// nothing else is left to report that the subscription died.
    /// </para>
    /// <para>
    /// Called from inside a <see cref="DeviceConnected"/> or <see cref="DeviceDisconnected"/>
    /// handler, it cannot wait for the handler that called it, so it returns once delivery of
    /// later events is stopped: the event being delivered still finishes with its remaining
    /// handlers, the loop unwinds as soon as they return, and a failure found while unwinding is
    /// logged at warning level rather than thrown, the disposer being no longer there to catch it.
    /// </para>
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Task? run;
        CancellationTokenSource? cts;
        // Decided before the first await: the method may resume on another thread afterwards, and
        // the comparison only means "inside a handler" on the thread that entered.
        var calledFromHandler = _dispatchThreadId == Environment.CurrentManagedThreadId;
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
#if NET8_0_OR_GREATER
            await cts.CancelAsync().ConfigureAwait(false);
#else
            cts.Cancel();
#endif
        }
        if (run is null)
        {
            // Nothing to wait for: never started, or run through RunAsync.
            return;
        }
        if (calledFromHandler)
        {
            // Waiting would deadlock: the loop is parked in the very handler running this code.
            // The loop sees _disposed as soon as that handler's event finishes dispatching, so the
            // tail of the shutdown rides on its completion instead of being awaited here.
            _ = FinishDisposalAsync(run, cts);
            return;
        }
        try
        {
            await run.ConfigureAwait(false);
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

    /// <summary>
    /// The tail of a disposal initiated from inside a handler: observes the subscription's end and
    /// releases the token source once the loop has unwound past the handler that disposed us.
    /// </summary>
    private async Task FinishDisposalAsync(Task run, CancellationTokenSource? cts)
    {
        try
        {
            // VSTHRD003: not foreign — this is our own subscription loop, already cancelled by
            // DisposeAsync, and awaiting it is how this tail knows when to release the sources.
#pragma warning disable VSTHRD003
            await run.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
        catch (OperationCanceledException)
        {
            // Same as the waiting path: cancellation is what disposal asked for.
        }
        catch (Exception ex)
        {
            // The waiting path rethrows, but the disposer has already moved on;
            // the log is the only report left.
            _logger.LogWarning(
                ex,
                "The hotplug subscription ended with a failure while disposal was unwinding it. "
                    + "{ErrorType}: {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private void Claim()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UsbHotplugEventNotifier));
            }
            if (_running)
            {
                throw new InvalidOperationException(
                    "This notifier has already been run. Construct another one for a second "
                        + "subscription."
                );
            }
            _running = true;
        }
    }

    private async Task SubscribeAsync(CancellationToken ct)
    {
        await foreach (var e in _monitor.Subscribe(_filter, ct).ConfigureAwait(false))
        {
            // Disposal stops delivery at once. Cancellation alone would not: an event already
            // read from the stream would still reach a handler while the loop unwinds.
            if (_disposed)
            {
                break;
            }
            var handler =
                e.Type == UsbHotplugEventType.Connected ? DeviceConnected : DeviceDisconnected;
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
}
