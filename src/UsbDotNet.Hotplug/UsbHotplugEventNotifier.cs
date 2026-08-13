using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <summary>
/// Raises classic .NET events for an <see cref="IUsbHotplugMonitor"/> subscription, for code that
/// would rather attach handlers than write an <c>await foreach</c>.
/// <para>
/// Construct it, attach handlers, then await <see cref="RunAsync"/> — in that order. The caller owns
/// the resulting task.
/// </para>
/// </summary>
public sealed class UsbHotplugEventNotifier
{
    private readonly IUsbHotplugMonitor _monitor;
    private readonly IUsbDeviceFilter? _filter;
    private readonly ILogger<UsbHotplugEventNotifier> _logger;

    // The only mutable state. See RunAsync.
    private int _running;

    /// <summary>Raised when a matching device is connected, or was already connected.</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceConnected;

    /// <summary>Raised when a matching device is disconnected.</summary>
    public event EventHandler<UsbHotplugEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Creates a notifier over <paramref name="monitor"/>. Nothing is subscribed yet; attach
    /// handlers and then call <see cref="RunAsync"/>.
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
    /// Subscribes and raises <see cref="DeviceConnected"/> / <see cref="DeviceDisconnected"/> until
    /// the subscription ends. Devices already connected when this is called are raised as
    /// <see cref="DeviceConnected"/> first, then live events follow.
    /// <para>
    /// <strong>Attach your handlers before calling this.</strong> The initial burst is delivered
    /// inside this method's synchronous prologue, so a handler attached after the call can miss it.
    /// </para>
    /// <para>
    /// This never returns normally — a live subscription has no natural end. Cancelling
    /// <paramref name="cancellationToken"/> throws an <see cref="OperationCanceledException"/>
    /// carrying that token; disposing the monitor or the underlying <see cref="IUsb"/> throws one
    /// carrying no token, with a message saying which. The exceptions documented on
    /// <see cref="IUsbHotplugMonitor.Subscribe"/> surface from here too, since this is what starts
    /// the subscription. Handler exceptions do <em>not</em> propagate: each handler is invoked
    /// individually and a thrower is logged at warning level, so one bad handler cannot deny the
    /// event to the others.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this notifier has already been run. The monitor's stream is reusable, but a
    /// second run over the same handlers would raise every event twice.
    /// </exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            throw new InvalidOperationException(
                "This notifier has already been run. Construct another one for a second "
                    + "subscription."
            );
        }
        await foreach (
            var e in _monitor.Subscribe(_filter, cancellationToken).ConfigureAwait(false)
        )
        {
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
