using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <inheritdoc/>
public sealed class UsbHotplugMonitor : IUsbHotplugMonitor, IHotplugListener
{
    private const string AlreadyRegisteredMessage =
        "Hotplug is already registered on this IUsb. Only one UsbHotplugMonitor may "
        + "be active per IUsb instance; share one monitor and add subscribers.";

    private const string ProviderDisposedMessage =
        "The underlying IUsb instance is disposed; hotplug monitoring has stopped. "
        + "Create a new IUsb instance and a new monitor to resume monitoring.";

    private const string MonitorDisposedMessage = "The UsbHotplugMonitor was disposed.";

    /// <summary>
    /// Guards registration and deregistration (see EnsureRegistered and Dispose). A separate lock
    /// because the provider dispatches into Dispatch (which takes _lock) both synchronously during
    /// RegisterHotplug and from the event loop thread; holding _lock across those provider calls
    /// would deadlock against a concurrent event.
    /// </summary>
    private readonly object _registerLock = new();

    /// <summary>
    /// Serializes _subscribers, the _connected devices snapshot, the _disposed/_providerDisposed
    /// lifecycle flags, and most importantly: every Dispatch from the provider.
    /// </summary>
    private readonly object _lock = new();

    private readonly IHotplugProvider _provider;
    private readonly ILogger<UsbHotplugMonitor> _logger;

    // Devices currently connected, keyed by DeviceKey. Mutated only under _lock, from the
    // hotplug events (libusb event loop thread) and read when a subscription starts.
    // Deliberately distinct from the provider's pointer-keyed device cache: this one is
    // maintained under _lock atomically with joining the fan-out, so a starting subscription's
    // snapshot is exactly consistent with the live stream (no duplicated and no missed events).
    // Snapshotting the provider's cache instead would race the in-flight dispatch that runs after
    // the cache is updated.
    private readonly Dictionary<string, IUsbDeviceDescriptor> _connected = [];

    // Live subscriptions
    private readonly Dictionary<Channel<UsbHotplugEvent>, IUsbDeviceFilter> _subscribers = [];

    private bool _registered;

    // Volatile because a consumer reads these without _lock before yielding each event
    private volatile bool _providerDisposed;
    private volatile bool _disposed;

    /// <summary>
    /// True when hotplug is supported on this platform. Enumerating
    /// <see cref="Subscribe(IUsbDeviceFilter?, CancellationToken)"/> throws
    /// <see cref="NotSupportedException"/> if hotplug is not supported.
    /// </summary>
    public bool IsHotplugSupported { get; }

    /// <summary>
    /// Creates a new <see cref="UsbHotplugMonitor"/> over the given <see cref="IUsb"/> instance.
    /// </summary>
    /// <param name="usb">
    /// The IUsb instance to monitor for hotplug events (must be of type <see cref="Usb"/>).
    /// </param>
    /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> to create loggers.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="usb"/> is not of type <see cref="Usb"/>,
    /// which is required to support hotplug registration.
    /// </exception>
    public static UsbHotplugMonitor Create(IUsb usb, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(usb);
        var usbWithProvider =
            usb as Usb
            ?? throw new ArgumentException(
                "The IUsb implementation must be of type Usb to support hotplug registration.",
                nameof(usb)
            );
        return new UsbHotplugMonitor(usbWithProvider.HotplugProvider, loggerFactory);
    }

    /// <summary>Live subscriptions.</summary>
    internal int SubscriptionCount
    {
        get
        {
            lock (_lock)
            {
                return _subscribers.Count;
            }
        }
    }

    /// <summary>Tracked connected devices.</summary>
    internal int ConnectedCount
    {
        get
        {
            lock (_lock)
            {
                return _connected.Count;
            }
        }
    }

    internal UsbHotplugMonitor(IHotplugProvider provider, ILoggerFactory? loggerFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = loggerFactory is null
            ? NullLogger<UsbHotplugMonitor>.Instance
            : loggerFactory.CreateLogger<UsbHotplugMonitor>();
        IsHotplugSupported = _provider.IsHotplugSupported;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An iterator which yields hotplug events for:
    /// 1. Devices already connected when the subscription starts
    /// 2. New events as they arrive from libusb
    ///
    /// Doesn't start running until the first consumer read.
    /// </remarks>
    public async IAsyncEnumerable<UsbHotplugEvent> Subscribe(
        IUsbDeviceFilter? filter = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // Before registering: an already-cancelled consumer should not cause a native registration
        // that nothing will ever read.
        cancellationToken.ThrowIfCancellationRequested();
        filter ??= UsbDeviceFilter.Any;
        EnsureRegistered();

        // Unbounded and never dropping while live: hotplug is low-volume, and dropping a connect
        // or disconnect would corrupt a consumer's view of device state. SingleWriter is false
        // because live events are written from the event loop thread while a concurrently starting
        // subscription can be writing from its own thread.
        var channel = Channel.CreateUnbounded<UsbHotplugEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );
        List<IUsbDeviceDescriptor> alreadyConnected;
        lock (_lock)
        {
            ThrowIfDisposed();
            ThrowIfProviderDisposed();

            // Snapshot the matching already connected devices.
            // These are yielded from the snapshot below
            alreadyConnected = _connected.Values.Where(d => filter.Matches(d)).ToList();
            _subscribers.Add(channel, filter);
        }

        _logger.LogDebug(
            "New subscriber with {Filter} registered; replaying {Connected} devices.",
            filter,
            alreadyConnected.Count
        );
        try
        {
            // first yield the already connected devices
            foreach (var descriptor in alreadyConnected)
            {
                ThrowIfTerminated();
                cancellationToken.ThrowIfCancellationRequested();
                yield return new UsbHotplugEvent(UsbHotplugEventType.Connected, descriptor);
            }

            // now yield the live events from libusb
            //
            // A completed writer hands its exception to WaitToReadAsync, so monitor and
            // provider teardown wake the consumer here. That is not sufficient on its own
            // as WaitToReadAsync reports a non-empty queue as readable regardless of the
            // writer being completed, which is what ThrowIfTerminated covers.
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var e))
                {
                    ThrowIfTerminated();
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return e;
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _ = _subscribers.Remove(channel);
            }
            _logger.LogDebug("Subscriber with {Filter} unsubscribed.", filter);
        }
    }

    /// <summary>
    /// Registers this monitor as the provider's hotplug listener on the first call. Serialized on
    /// _registerLock rather than _lock: the provider dispatches the LIBUSB_HOTPLUG_ENUMERATE
    /// replay synchronously during RegisterHotplug while holding its dispatch lock, and that
    /// dispatch takes _lock in Dispatch. Holding _lock across RegisterHotplug would invert the
    /// provider's dispatch-lock/_lock order and deadlock against a concurrent live event.
    /// </summary>
    private void EnsureRegistered()
    {
        lock (_registerLock)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                ThrowIfProviderDisposed();
            }
            if (_registered)
            {
                return;
            }
            try
            {
                _registered = _provider.RegisterHotplug(this) switch
                {
                    HotplugRegistrationResult.Success => true,
                    HotplugRegistrationResult.NotSupported => throw new NotSupportedException(
                        "Hotplug is not supported on this platform."
                    ),
                    HotplugRegistrationResult.AlreadyRegistered =>
                        throw new InvalidOperationException(AlreadyRegisteredMessage),
                };
            }
            catch (ObjectDisposedException ex)
            {
                // The provider is disposing/disposed concurrently: it signals Disposing at the
                // start of its teardown but notifies OnProviderDisposed only at the end, so this
                // exception is the only way to observe the window in between.
                lock (_lock)
                {
                    _providerDisposed = true;
                }
                throw new InvalidOperationException(ProviderDisposedMessage, ex);
            }
        }
    }

    void IHotplugListener.OnDeviceArrived(IUsbDeviceDescriptor descriptor) =>
        Dispatch(UsbHotplugEventType.Connected, descriptor);

    void IHotplugListener.OnDeviceLeft(IUsbDeviceDescriptor descriptor) =>
        Dispatch(UsbHotplugEventType.Disconnected, descriptor);

    /// <summary>
    /// Runs inside the provider's hotplug dispatch: on the libusb event-loop thread for live
    /// events and on the registering thread during the LIBUSB_HOTPLUG_ENUMERATE replay.
    /// <para>
    /// NOTE: Subscriber code must never execute synchronously from here; the channel writes
    /// below resume readers on the thread pool. Usb.Dispose relies on this to guarantee it is
    /// never called from inside a dispatch, where it would deadlock (see the note at the top
    /// of Usb.Dispose).
    /// </para>
    /// </summary>
    private void Dispatch(UsbHotplugEventType type, IUsbDeviceDescriptor descriptor)
    {
        // Filter out devices with a synthesized descriptor (BcdUsb == 0), typically root hubs and
        // devices with an unreadable descriptor. Aligns with Usb.GetDeviceList implementation.
        if (!descriptor.HasValidBcdUsb())
        {
            return;
        }
        lock (_lock)
        {
            if (_disposed || _providerDisposed)
            {
                return;
            }
            if (type == UsbHotplugEventType.Connected)
            {
                _connected[descriptor.DeviceKey] = descriptor;
            }
            else
            {
                _ = _connected.Remove(descriptor.DeviceKey);
            }
            foreach (var (channel, subscriberFilter) in _subscribers)
            {
                if (subscriberFilter.Matches(descriptor))
                {
                    _ = channel.Writer.TryWrite(new UsbHotplugEvent(type, descriptor));
                }
            }
        }
    }

    /// <summary>
    /// The underlying IUsb has completed its teardown: no further hotplug events can arrive and
    /// the tracked snapshot no longer reflects reality. Drop the snapshot, terminate all live
    /// subscriptions (undelivered events are dropped and each consumer's await foreach throws an
    /// <see cref="OperationCanceledException"/>, since events describing devices the disposed IUsb
    /// can no longer reach make no sense to deliver), and let a starting subscription throw
    /// instead of replaying stale devices.
    /// </summary>
    void IHotplugListener.OnProviderDisposed()
    {
        List<Channel<UsbHotplugEvent>> channels;
        lock (_lock)
        {
            if (_disposed || _providerDisposed)
            {
                return;
            }
            _providerDisposed = true;
            channels = [.. _subscribers.Keys];
            _subscribers.Clear();
            _connected.Clear();
        }
        _logger.LogInformation(
            "The underlying IUsb instance was disposed; hotplug monitoring stopped and all "
                + "subscriptions were canceled."
        );
        Terminate(channels, ProviderDisposedMessage);
    }

    /// <summary>
    /// Wakes every consumer of <paramref name="channels"/> with a cancellation. Completing a writer
    /// with an <see cref="OperationCanceledException"/> hands that exact instance to a consumer
    /// parked in WaitToReadAsync and to every later read, so the wake-up is immediate.
    /// </summary>
    private static void Terminate(List<Channel<UsbHotplugEvent>> channels, string reason)
    {
        foreach (var channel in channels)
        {
            _ = channel.Writer.TryComplete(new OperationCanceledException(reason));
        }
    }

    /// <summary>
    /// Throws when the monitor or the underlying IUsb was disposed
    /// </summary>
    private void ThrowIfTerminated()
    {
        // Provider first: it is the more specific reason, and Dispose can still run afterwards and
        // set its own flag on top.
        if (_providerDisposed)
        {
            throw new OperationCanceledException(ProviderDisposedMessage);
        }
        if (_disposed)
        {
            throw new OperationCanceledException(MonitorDisposedMessage);
        }
    }

    /// <summary>
    /// Releases the hotplug registration this monitor owns (allowing a new monitor over the same
    /// <see cref="IUsb"/> instance) and terminates all live subscriptions: undelivered events are
    /// dropped and each consumer's await foreach throws an
    /// <see cref="OperationCanceledException"/> rather than ending quietly.
    /// Does not dispose the <see cref="IUsb"/> instance.
    /// </summary>
    public void Dispose()
    {
        List<Channel<UsbHotplugEvent>> channels;
        // _registerLock serializes this with an in-flight EnsureRegistered, so a registration
        // completed by a racing subscription is always observed and released here.
        lock (_registerLock)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                channels = [.. _subscribers.Keys];
                _subscribers.Clear();
                _connected.Clear();
            }
            // Deregister outside _lock: DeregisterHotplug waits for any in-flight listener
            // invocation, which takes _lock in Dispatch; holding _lock here would deadlock.
            // Once it returns the provider never invokes this monitor again.
            if (_registered)
            {
                _provider.DeregisterHotplug(this);
            }
        }
        // Terminate rather than end quietly: parked consumers wake immediately, later reads throw,
        // and undelivered events are refused, so consumers can tell monitor disposal apart from
        // their own stop and never act on a device the monitor no longer tracks.
        Terminate(channels, MonitorDisposedMessage);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UsbHotplugMonitor));
        }
    }

    private void ThrowIfProviderDisposed()
    {
        if (_providerDisposed)
        {
            throw new InvalidOperationException(ProviderDisposedMessage);
        }
    }
}
