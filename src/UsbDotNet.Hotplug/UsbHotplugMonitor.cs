using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug;

/// <inheritdoc/>
public sealed class UsbHotplugMonitor : IUsbHotplugMonitor
{
    private readonly object _lock = new();
    private readonly IHotplugProvider _provider;
    private readonly ILogger<UsbHotplugMonitor> _logger;

    // Devices currently connected, keyed by DeviceKey. Mutated only under _lock, from the
    // hotplug events (libusb event loop thread) and read when replaying to late subscribers.
    private readonly Dictionary<string, IUsbDeviceDescriptor> _connected = [];
    private readonly List<Subscription> _subscriptions = [];

    private bool _started;
    private bool _providerDisposed;
    private bool _disposed;

    /// <summary>
    /// Whether hotplug is supported on this platform. Determined at construction; when
    /// <see langword="false"/>, subscriptions are created but never receive events.
    /// </summary>
    public bool IsHotplugSupported { get; }

    /// <summary>
    /// Creates a monitor over the given, externally owned and initialized, <see cref="IUsb"/>.
    /// The monitor does not create, initialize or dispose the <see cref="IUsb"/> instance.
    /// </summary>
    /// <param name="usb">The USB instance to monitor. NOTE: Initialize before subscribing!</param>
    /// <param name="loggerFactory">Optional logger factory. If null, logging is disabled.</param>
    public UsbHotplugMonitor(IUsb usb, ILoggerFactory? loggerFactory = null)
        : this(AsHotplugProvider(usb), loggerFactory) { }

    // The monitor is built from the hotplug provider; the public constructor takes IUsb only
    // because IHotplugProvider is internal (it cannot appear in a public signature) and consumers
    // only ever hold an IUsb.
    internal UsbHotplugMonitor(IHotplugProvider provider, ILoggerFactory? loggerFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = loggerFactory is null
            ? NullLogger<UsbHotplugMonitor>.Instance
            : loggerFactory.CreateLogger<UsbHotplugMonitor>();
        IsHotplugSupported = _provider.IsHotplugSupported;
        // Attach before registering (which happens on first Subscribe)
        // so the enumeration of already connected devices is never missed.
        _provider.DeviceArrived += OnDeviceArrived;
        _provider.DeviceLeft += OnDeviceLeft;
        _provider.Disposed += OnProviderDisposed;
    }

    private static IHotplugProvider AsHotplugProvider(IUsb usb)
    {
        ArgumentNullException.ThrowIfNull(usb);
        return usb as IHotplugProvider
            ?? throw new ArgumentException(
                "The IUsb implementation does not support hotplug registration.",
                nameof(usb)
            );
    }

    /// <inheritdoc/>
    public IUsbHotplugSubscription Subscribe(IUsbDeviceFilter? filter = null)
    {
        filter ??= UsbDeviceFilter.Any;
        lock (_lock)
        {
            ThrowIfDisposed();
            if (_providerDisposed)
            {
                throw new InvalidOperationException(
                    "The underlying IUsb instance is disposed; hotplug monitoring has stopped. "
                        + "Create a new IUsb instance and a new monitor to resume monitoring."
                );
            }

            // Register the native callback once, before adding the subscriber. libusb delivers the
            // LIBUSB_HOTPLUG_ENUMERATE events asynchronously on its event loop thread, which must
            // take _lock to dispatch; since we hold _lock until the subscriber is added below, no
            // enumeration event can be dispatched before the first subscriber is in the list.
            if (!_started)
            {
                if (_provider.RegisterHotplug() is HotplugRegistrationResult.AlreadyRegistered)
                {
                    throw new InvalidOperationException(
                        "Hotplug is already registered on this IUsb. Only one UsbHotplugMonitor may "
                            + "be active per IUsb instance; share one monitor and add subscribers."
                    );
                }
                if (!IsHotplugSupported)
                {
                    _logger.LogWarning(
                        "Hotplug is not supported on this platform; no events will be emitted."
                    );
                }
                _started = true;
            }

            var subscription = new Subscription(this, filter);
            // Replay currently connected, matching devices as Connected events (per-subscriber
            // enumeration). Empty for the very first subscriber; libusb enumeration then populates
            // _connected and reaches this subscriber via the live dispatch path.
            foreach (var descriptor in _connected.Values)
            {
                if (filter.Matches(descriptor))
                {
                    subscription.Write(UsbHotplugEventType.Connected, descriptor);
                }
            }
            _subscriptions.Add(subscription);
            return subscription;
        }
    }

    private void OnDeviceArrived(object? sender, IUsbDeviceDescriptor descriptor) =>
        Dispatch(UsbHotplugEventType.Connected, descriptor);

    private void OnDeviceLeft(object? sender, IUsbDeviceDescriptor descriptor) =>
        Dispatch(UsbHotplugEventType.Disconnected, descriptor);

    private void Dispatch(UsbHotplugEventType type, IUsbDeviceDescriptor descriptor)
    {
        // Devices no filter can ever match (synthesized descriptors, see UsbDeviceFilter) are
        // neither tracked in _connected nor delivered to any subscriber.
        if (!UsbDeviceFilter.Any.Matches(descriptor))
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
    /// the tracked snapshot no longer reflects reality. Drop the snapshot, complete all
    /// subscription channels so consumers observe a clean end-of-stream, and let Subscribe reject
    /// new subscribers instead of replaying stale devices.
    /// </summary>
    private void OnProviderDisposed(object? sender, EventArgs e)
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
                + "subscriptions were completed."
        );
        foreach (var subscription in subscriptions)
        {
            subscription.Complete();
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
    /// Detaches from the <see cref="IUsb"/> hotplug events and completes all subscription channels.
    /// Does not deregister the native hotplug callback or dispose the <see cref="IUsb"/> instance;
    /// the native callback is deregistered when the <see cref="IUsb"/> instance itself is disposed.
    /// </summary>
    public void Dispose()
    {
        List<Subscription> subscriptions;
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
        // Detach outside _lock: the provider's event accessors take the Usb instance lock, and
        // holding _lock across that call deadlocks against the libusb event-loop thread, which
        // blocks on _lock in Dispatch while a disposing Usb instance waits for it to exit.
        // Events dispatched before the detach completes are ignored by the _disposed check.
        _provider.DeviceArrived -= OnDeviceArrived;
        _provider.DeviceLeft -= OnDeviceLeft;
        _provider.Disposed -= OnProviderDisposed;
        foreach (var subscription in subscriptions)
        {
            subscription.Complete();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UsbHotplugMonitor));
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
            // Unbounded and never dropping: hotplug is low-volume, and dropping a connect or
            // disconnect would corrupt a consumer's view of device state. Multiple writer threads
            // (event loop thread for live events, subscribing thread for the initial replay).
            _channel = Channel.CreateUnbounded<UsbHotplugEvent>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
            );
        }

        // Called while the monitor holds _lock, so writes to a live subscription are serialized.
        public void Write(UsbHotplugEventType type, IUsbDeviceDescriptor descriptor) =>
            _channel.Writer.TryWrite(new UsbHotplugEvent(type, descriptor));

        public void Complete() => _channel.Writer.TryComplete();

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
