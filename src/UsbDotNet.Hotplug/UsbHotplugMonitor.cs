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

    /// <summary>
    /// Guards registration and deregistration (see EnsureRegistered and Dispose). A separate lock
    /// because the provider dispatches into Dispatch (which takes _lock) both synchronously during
    /// RegisterHotplug and from the event loop thread; holding _lock across those provider calls
    /// would deadlock against a concurrent event.
    /// </summary>
    private readonly object _registerLock = new();

    /// <summary>
    /// Serializes _subscriptions, the _connected devices snapshot, the _disposed/_providerDisposed
    /// lifecycle flags, and most importantly: every Dispatch from the provider.
    /// </summary>
    private readonly object _lock = new();

    private readonly IHotplugProvider _provider;
    private readonly ILogger<UsbHotplugMonitor> _logger;

    // Devices currently connected, keyed by DeviceKey. Mutated only under _lock, from the
    // hotplug events (libusb event loop thread) and read when replaying to late subscribers.
    // Deliberately distinct from the provider's pointer-keyed device cache: this one is
    // maintained under _lock atomically with the subscription channel writes, so a late
    // subscriber's replay is exactly consistent with the live stream (no duplicated and no
    // missed events). Snapshotting the provider's cache instead would race the in-flight
    // dispatch that runs after the cache is updated.
    private readonly Dictionary<string, IUsbDeviceDescriptor> _connected = [];
    private readonly List<Subscription> _subscriptions = [];

    private bool _registered;
    private bool _providerDisposed;
    private bool _disposed;

    /// <summary>
    /// True when hotplug is supported on this platform. <see cref="Subscribe(IUsbDeviceFilter?)"/>
    /// throws <see cref="NotSupportedException"/> if hotplug is not supported.
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

    internal UsbHotplugMonitor(IHotplugProvider provider, ILoggerFactory? loggerFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = loggerFactory is null
            ? NullLogger<UsbHotplugMonitor>.Instance
            : loggerFactory.CreateLogger<UsbHotplugMonitor>();
        IsHotplugSupported = _provider.IsHotplugSupported;
    }

    /// <inheritdoc/>
    public IUsbHotplugSubscription Subscribe(IUsbDeviceFilter? filter = null)
    {
        filter ??= UsbDeviceFilter.Any;
        EnsureRegistered();
        lock (_lock)
        {
            ThrowIfDisposed();
            ThrowIfProviderDisposed();

            var subscription = new Subscription(this, filter);
            // Replay currently connected, matching devices as Connected events (per-subscriber
            // enumeration), then add the subscription — atomically under one _lock acquisition:
            // events dispatched before this point are reflected in _connected, events after
            // reach the subscription via Dispatch, so the replay is exactly consistent with the
            // live stream. For the very first subscriber _connected was populated by the
            // synchronous LIBUSB_HOTPLUG_ENUMERATE replay inside EnsureRegistered.
            var matchingDevices = _connected.Values.Where(d => filter.Matches(d)).ToList();
            _logger.LogDebug(
                "New subscriber with {Filter} registered; replaying {Connected} devices.",
                filter,
                matchingDevices.Count
            );
            foreach (var descriptor in matchingDevices)
            {
                subscription.Write(UsbHotplugEventType.Connected, descriptor);
            }
            _subscriptions.Add(subscription);
            return subscription;
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
            foreach (var subscription in _subscriptions)
            {
                if (subscription.Filter.Matches(descriptor))
                {
                    subscription.Write(type, descriptor);
                }
            }
        }
    }

    /// <summary>
    /// The underlying IUsb has completed its teardown: no further hotplug events can arrive and
    /// the tracked snapshot no longer reflects reality. Drop the snapshot, abort all subscription
    /// channels (undelivered events are dropped and blocked readers wake to a cancellation, since
    /// events describing devices the disposed IUsb can no longer reach make no sense to deliver),
    /// and let Subscribe reject new subscribers instead of replaying stale devices.
    /// </summary>
    void IHotplugListener.OnProviderDisposed()
    {
        List<Subscription> subscriptions;
        lock (_lock)
        {
            if (_disposed || _providerDisposed)
            {
                return;
            }
            _providerDisposed = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
            _connected.Clear();
        }
        _logger.LogInformation(
            "The underlying IUsb instance was disposed; hotplug monitoring stopped and all "
                + "subscriptions were canceled."
        );
        foreach (var subscription in subscriptions)
        {
            subscription.Abort(new OperationCanceledException(ProviderDisposedMessage));
        }
    }

    private void Unsubscribe(Subscription subscription)
    {
        lock (_lock)
        {
            _ = _subscriptions.Remove(subscription);
        }
        subscription.Complete();
    }

    /// <summary>
    /// Releases the hotplug registration this monitor owns (allowing a new monitor over the same
    /// <see cref="IUsb"/> instance) and aborts all subscription channels: undelivered events are
    /// dropped and pending and future reads are canceled with
    /// <see cref="OperationCanceledException"/> rather than observing a clean end-of-stream.
    /// Does not dispose the <see cref="IUsb"/> instance.
    /// </summary>
    public void Dispose()
    {
        List<Subscription> subscriptions;
        // _registerLock serializes this with an in-flight EnsureRegistered, so a registration
        // completed by a racing Subscribe is always observed and released here.
        lock (_registerLock)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                subscriptions = [.. _subscriptions];
                _subscriptions.Clear();
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
        foreach (var subscription in subscriptions)
        {
            // Abort rather than complete: blocked readers wake immediately, future reads throw,
            // and undelivered events are dropped, so consumers can tell monitor disposal apart
            // from a clean end-of-stream and never act on a device the monitor no longer tracks.
            // A fresh exception per subscription since each reader rethrows it independently.
            subscription.Abort(
                new OperationCanceledException("The UsbHotplugMonitor was disposed.")
            );
        }
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

    private sealed class Subscription : IUsbHotplugSubscription
    {
        private readonly UsbHotplugMonitor _monitor;
        private readonly Channel<UsbHotplugEvent> _channel;
        private int _disposed;

        public IUsbDeviceFilter Filter { get; }

        public ChannelReader<UsbHotplugEvent> Reader => _channel.Reader;

        public Subscription(UsbHotplugMonitor monitor, IUsbDeviceFilter filter)
        {
            _monitor = monitor;
            Filter = filter;
            // Unbounded and never dropping while live: hotplug is low-volume, and dropping a
            // connect or disconnect would corrupt a consumer's view of device state. Multiple
            // writer threads (event loop thread for live events, subscribing thread for the
            // initial replay). SingleReader is false because Abort drains the buffer from the
            // disposing thread, potentially concurrent with a consumer read.
            // AllowSynchronousContinuations must stay at its default (false): with it enabled a
            // blocked reader's continuation could run on the writing thread, i.e. consumer code
            // inside a hotplug dispatch, breaking the no-user-code-in-dispatch rule that
            // Usb.Dispose depends on (see the note at the top of Usb.Dispose).
            _channel = Channel.CreateUnbounded<UsbHotplugEvent>(
                new UnboundedChannelOptions { SingleReader = false, SingleWriter = false }
            );
        }

        // Called while the monitor holds _lock, so writes to a live subscription are serialized.
        public void Write(UsbHotplugEventType type, IUsbDeviceDescriptor descriptor) =>
            _channel.Writer.TryWrite(new UsbHotplugEvent(type, descriptor));

        public void Complete() => _channel.Writer.TryComplete();

        /// <summary>
        /// Cancels the subscription: pending and future reads observe <paramref name="error"/>,
        /// and buffered events are dropped — they describe devices the disposed monitor or IUsb
        /// can no longer reach. TryComplete stops new writes, so the drain terminates and the
        /// error surfaces to the consumer immediately instead of after stale events. A consumer
        /// read racing the drain may still win one already-buffered event, which is
        /// indistinguishable from having read it just before the dispose.
        /// </summary>
        public void Abort(OperationCanceledException error)
        {
            _ = _channel.Writer.TryComplete(error);
            while (_channel.Reader.TryRead(out _)) { }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            _monitor.Unsubscribe(this);
        }
    }
}
