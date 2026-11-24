using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Internal;

/// <summary>
/// Owns the hotplug registration state and event dispatch for a <see cref="Usb"/> instance: the
/// native callback handle, the attached <see cref="IUsbHotplugListener"/> and the arrived-device
/// cache. Context access and disposal checks go through <see cref="IUsbInternal"/>; teardown is
/// orchestrated by <see cref="Usb.Dispose"/> via <see cref="Shutdown"/> and
/// <see cref="NotifyProviderDisposed"/>.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
// CA1001: teardown is owned by Usb.Dispose (Shutdown -> NotifyProviderDisposed).
// Implementing IDisposable would make the DI container, which tracks
// every IDisposable it resolves, dispose the provider a second time behind Usb's back.
internal sealed class HotplugProvider : IUsbHotplugProvider
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly IUsbInternal _usb;
    private readonly ILogger _logger;

    private IUsbHotplugListener? _listener;

    // Serializes IUsbHotplugListener invocations across their two dispatch sources: the enumeration
    // replay libusb runs synchronously inside libusb_hotplug_register_callback (on the registering
    // thread) and live events from libusb_handle_events (on the event loop thread). Registration
    // and deregistration acquire it BEFORE the usb's lifetime lock (entered via
    // IUsbInternal.WithInitializedContext), so the global lock order is
    // _dispatchLock -> usb lifetime lock; the dispatch path must never enter the usb.
    private readonly object _dispatchLock = new();

    /// <summary>
    /// Devices seen via hotplug DEVICE_ARRIVED, keyed by their libusb_device pointer. Each entry
    /// keeps the ISafeDevice (and a libusb reference) taken on arrival, so that on DEVICE_LEFT we
    /// can recover the full descriptor.
    /// <para>
    /// A DeviceKey needs VID/PID plus bus number/address. VID/PID alone cannot distinguish two
    /// identical devices plugged in at the same time; the bus number/address makes the key unique.
    /// </para>
    /// <para>
    /// On DEVICE_ARRIVED reading bus number/address is fine, On DEVICE_LEFT it is not. According to
    /// the docs only libusb_get_device_descriptor(VID/PID) is safe to call on DEVICE_LEFT. See:
    /// https://libusb.sourceforge.io/api-1.0/libusb_hotplug.html. The libusb_device pointer,
    /// however, is stable: libusb passes the same device object to both the arrival and removal
    /// callbacks (verified in v1.0.30 core.c/hotplug.c). So we use it as an opaque identifier
    /// <see cref="SafeDevice.Id"/> and do a look up of the cached descriptor on DEVICE_LEFT.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<
        UniqueId,
        (ISafeDevice Device, UsbDeviceDescriptor Descriptor)
    > _devices = new();

#pragma warning disable CA2213 // Disposable fields should be disposed
    // CA2213 false positive: Shutdown and DeregisterHotplug take ownership of the handle via
    // Interlocked.Exchange and dispose it through the local.
    private ISafeCallbackHandle? _callbackHandle;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private readonly RundownGuard _callbackRundown = new();

    // The managed thread that is inside a listener notification, or 0. Written under
    // _dispatchLock, read without it by IsDispatchingOnCurrentThread.
    private int _dispatchThreadId;

    // The listener that is inside the OnProviderDisposed notification, and the managed thread
    // raising it; null and 0 otherwise. Guarded by _dispatchLock. DeregisterHotplug of that
    // listener waits on the lock until the notification has returned.
    private IUsbHotplugListener? _providerDisposedListener;
    private int _providerDisposedThreadId;

    /// <summary>
    /// True when the calling thread is inside an <see cref="IUsbHotplugListener.OnDeviceArrived"/>
    /// or <see cref="IUsbHotplugListener.OnDeviceLeft"/> notification of this provider. Covers
    /// both dispatch sources: live events and the enumeration replay.
    /// </summary>
    internal bool IsDispatchingOnCurrentThread =>
        Volatile.Read(ref _dispatchThreadId) == Environment.CurrentManagedThreadId;

    internal HotplugProvider(IUsbInternal usb, ILogger logger)
    {
        _usb = usb ?? throw new ArgumentNullException(nameof(usb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    bool IUsbHotplugProvider.IsHotplugSupported => _usb.IsHotplugSupported;

    /// <inheritdoc/>
    UsbHotplugRegistrationResult IUsbHotplugProvider.RegisterHotplug(IUsbHotplugListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        // Taken before the usb lifetime lock; all registration paths preserve the
        // _dispatchLock -> usb lock order. The enumeration replay re-enters the dispatch lock on
        // this thread from HotplugEventCallback, while live events on the event loop thread wait
        // until the registration (including its replay) completes.
        lock (_dispatchLock)
        {
            // The lifetime check comes first: a listener stays attached through and after
            // Usb.Dispose, and a disposed instance must throw rather than report it.
            return _usb.WithInitializedContext(context => RegisterUnlocked(context, listener));
        }
    }

    /// <inheritdoc/>
    void IUsbHotplugProvider.DeregisterHotplug(IUsbHotplugListener listener)
    {
        // Deliberately runs without a disposed check: the cleanup path (typically
        // UsbHotplugMonitor.Dispose) may race Usb.Dispose; when Shutdown already took the callback
        // handle every step below is a safe no-op.
        ArgumentNullException.ThrowIfNull(listener);
        // Holding the dispatch lock makes deregistration a clean cutoff: any in-flight listener
        // invocation holds this lock, so we wait for it, and any dispatch blocked acquiring it
        // observes the cleared listener afterwards and drops the event.
        lock (_dispatchLock)
        {
            // Nothing to release when the listener is not the registration owner (or nothing is
            // registered).
            if (ReferenceEquals(_listener, listener))
            {
                // The lock is reentrant, so the owner gets here from inside its own notification.
                // During the enumeration replay the callback handle is not stored yet: clearing
                // the listener now would leave a live native registration that nobody owns.
                if (_dispatchThreadId == Environment.CurrentManagedThreadId)
                {
                    throw new InvalidOperationException(
                        "A hotplug listener cannot be deregistered from within its own "
                            + "notification. Deregister it from another thread, after the "
                            + "notification has returned."
                    );
                }
                _listener = null;
                // The exchange serializes handle ownership with Shutdown, which must not wait on
                // _dispatchLock (an in-flight listener invocation may hold it).
                var handle = Interlocked.Exchange(ref _callbackHandle, null);
                handle?.Dispose();
                ReleaseDeviceCache();
            }
            // OnProviderDisposed is raised outside the lock, so it may still be running on this
            // listener. Wait for it (the wait releases the lock), unless this is the listener
            // deregistering from inside that notification. Checked for a listener that is no
            // longer the owner too: an earlier deregistration that is still waiting here has
            // already detached it.
            while (
                ReferenceEquals(_providerDisposedListener, listener)
                && _providerDisposedThreadId != Environment.CurrentManagedThreadId
            )
            {
                _ = Monitor.Wait(_dispatchLock);
            }
        }
    }

    /// <summary>
    /// Deregisters the native callback, waits for any in-flight hotplug callback to finish and
    /// releases the cached device references. Called from <see cref="Usb.Dispose"/> after the
    /// dispose state is set, so no new registration can recreate the handle. Must not take
    /// <see cref="_dispatchLock"/>: an in-flight listener invocation holds it, and the rundown
    /// wait below is the mechanism that waits for it. <see cref="NotifyProviderDisposed"/>
    /// remains safe to call afterwards; it completes the teardown at the end of
    /// <see cref="Usb.Dispose"/>.
    /// </summary>
    internal void Shutdown()
    {
        var handle = Interlocked.Exchange(ref _callbackHandle, null);
        handle?.Dispose();
        _callbackRundown.Dispose();
        // Only after the rundown wait is the device cache frozen: an in-flight dispatch may still
        // add to it, while a later callback invocation fails the token acquisition and never
        // touches it. Releasing here (before Usb.Dispose reaches libusb_exit) drops the last
        // libusb device references held on behalf of this registration.
        ReleaseDeviceCache();
    }

    /// <summary>
    /// Releases the cached hotplug device references (and their libusb refs). Without this, a
    /// later registration's LIBUSB_HOTPLUG_ENUMERATE replay would be suppressed by the
    /// duplicate-arrival check in HandleDeviceArrived, and the cached devices would keep the
    /// native context referenced.
    /// </summary>
    private void ReleaseDeviceCache()
    {
        foreach (var entry in _devices.ToArray())
        {
            if (_devices.TryRemove(entry.Key, out var cached))
            {
                cached.Device.Dispose();
            }
        }
    }

    /// <summary>
    /// Raises <see cref="IUsbHotplugListener.OnProviderDisposed"/> on the attached listener, if any.
    /// Called at the end of <see cref="Usb.Dispose"/>, after teardown completed. The callback is
    /// raised outside the dispatch lock, as the listener contract promises that no locks are held.
    /// A DeregisterHotplug of the listener either detaches it before it is read here, so it is
    /// not notified, or waits until the notification has returned.
    /// </summary>
    internal void NotifyProviderDisposed()
    {
        IUsbHotplugListener? listener;
        lock (_dispatchLock)
        {
            listener = _listener;
            if (listener is null)
            {
                return;
            }
            _providerDisposedListener = listener;
            _providerDisposedThreadId = Environment.CurrentManagedThreadId;
        }
        try
        {
            EventDispatch.RaiseSafely(listener.OnProviderDisposed, _logger);
        }
        finally
        {
            lock (_dispatchLock)
            {
                _providerDisposedListener = null;
                _providerDisposedThreadId = 0;
                Monitor.PulseAll(_dispatchLock);
            }
        }
    }

    /// <summary>
    /// Attaches the <paramref name="listener"/>, registers the native hotplug callback for all
    /// devices and stores its handle. The caller must hold <see cref="_dispatchLock"/> and the
    /// usb lifetime lock (see <see cref="IUsbInternal.WithInitializedContext{T}"/>). With no
    /// listener attached there is no callback handle either, since the handle is only set here
    /// and is cleared together with the listener.
    /// </summary>
    private UsbHotplugRegistrationResult RegisterUnlocked(
        ISafeContext context,
        IUsbHotplugListener listener
    )
    {
        // Reject a second listener before touching the slot so a briefly swapped-in
        // listener can never be handed an event that belongs to the current owner.
        if (_listener is not null)
        {
            return UsbHotplugRegistrationResult.AlreadyRegistered;
        }
        if (!_usb.IsHotplugSupported)
        {
            _logger.LogDebug("Hotplug not supported or unimplemented on this platform.");
            return UsbHotplugRegistrationResult.NotSupported;
        }
        // Attach before registering: the enumeration replay must reach the listener.
        _listener = listener;
        try
        {
            // We do not follow the recommended libusb init pattern: hotplug first then event
            // loop. See:
            // https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
            // This should not have any adverse effects as long as we register callback with
            // the LibUsbHotplugFlag.Enumerate flag, as it allows catching up with current
            // devices.
            _callbackHandle = context.RegisterHotplugCallback(
                libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
                    | libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT,
                // Set flag LibUsbHotplugFlag.Enumerate to immediately invoke the
                // HotplugEventCallback method for currently attached devices on register
                libusb_hotplug_flag.LIBUSB_HOTPLUG_ENUMERATE,
                HotplugEventCallback
            );
        }
        catch
        {
            _listener = null;
            throw;
        }
        return UsbHotplugRegistrationResult.Success;
    }

    /// <summary>
    /// <para>
    /// NOTE:
    /// This callback runs on the LibUsbEventLoop thread for live events, and on the registering
    /// thread for the LIBUSB_HOTPLUG_ENUMERATE replay; invocations are serialized by
    /// _dispatchLock.
    /// </para>
    /// <para>
    /// When handling a DEVICE_ARRIVED event it's considered safe to call any libusb function that
    /// takes a libusb_device. It is also safe to open a device and submit asynchronous transfers.
    /// However, most other functions that take a libusb_device_handle are not safe to call.
    /// Examples of such functions are any of the synchronous API functions or the blocking
    /// functions that retrieve various USB descriptors.
    /// See: https://libusb.sourceforge.io/api-1.0/group__libusb__desc.html
    /// These functions must be used outside of the context of the hotplug callback.
    /// When handling a DEVICE_LEFT event the only safe function is libusb_get_device_descriptor().
    /// </para>
    /// </summary>
    private libusb_hotplug_return HotplugEventCallback(
        ISafeContext context,
        ISafeDevice device,
        libusb_hotplug_event eventType
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(device);
        try
        {
            using var token = _callbackRundown.AcquireSharedToken();
            // Serialize the two dispatch sources (enumeration replay on the registering thread,
            // live events on the event loop thread) so the IUsbHotplugListener is never invoked
            // concurrently. Reentrant for the replay, whose registering thread already holds it.
            lock (_dispatchLock)
            {
                // NOTE: The event handlers are implemented and expected to never throw.
                if (eventType is libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED)
                {
                    HandleDeviceArrived(device);
                }
                else
                {
                    HandleDeviceLeft(device);
                }
            }
            return libusb_hotplug_return.REARM;
        }
        // ObjectDisposedException is thrown when the hotplug rundown guard dispose has started,
        // which means the Usb instance is being disposed. In this case we silently dispose the
        // newly arrived device and deregister the callback.
        catch (ObjectDisposedException)
        {
            device.Dispose();
            return libusb_hotplug_return.DEREGISTER;
        }
    }

    /// <summary>
    /// NOTE: This method is implemented and expected to never throw.
    /// </summary>
    private void HandleDeviceArrived(ISafeDevice device)
    {
        UsbDeviceDescriptor descriptor;
        try
        {
            descriptor = UsbDeviceDescriptor.FromDevice(device);
        }
        // NOTE: Never throws; since libusb-1.0.16 libusb_get_device_descriptor always succeeds
        catch (UsbException ex)
        {
            _logger.LogError("Hotplug event handling failed. {ErrorMessage}.", ex.Message);
            device.Dispose();
            return;
        }
        // Cache the arriving device, keeping the libusb reference held by this ISafeDevice,
        // so the descriptor (including bus number/address) can be recovered on DEVICE_LEFT.
        if (!_devices.TryAdd(device.Id, (device, descriptor)))
        {
            // With LIBUSB_HOTPLUG_ENUMERATE libusb may notify the arrival of the same device
            // twice: once from registration enumeration and once from the live event loop.
            device.Dispose();
            return;
        }
        EmitHotplugEvent(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED, descriptor);
    }

    /// <summary>
    /// NOTE: This method is implemented and expected to never throw.
    /// </summary>
    private void HandleDeviceLeft(ISafeDevice device)
    {
        // NOTE: The SafeDevice received on DEVICE_LEFT is not the same SafeDevice as the one
        // received on DEVICE_ARRIVED, even though the underlying libusb_device pointer is the same.
        // SafeContext creates a new SafeDevice for each callback, both must be disposed here.
        if (_devices.TryRemove(device.Id, out var cached))
        {
            device.Dispose(); // Dispose the throwaway instance created for the DEVICE_LEFT callback
            cached.Device.Dispose(); // Release the reference taken on arrival
            EmitHotplugEvent(
                libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT,
                cached.Descriptor
            );
            return;
        }
        // A device we never cached (per docs: removal may be notified without a prior arrival).
        // Unlikely, but possible despite LIBUSB_HOTPLUG_ENUMERATE: RegisterHotplug can race the
        // event loop and enumerate before a newly arrived device becomes visible.
        device.Dispose(); // Dispose the throwaway instance created for the callback
    }

    /// <summary>
    /// NOTE: This method is implemented and expected to never throw.
    /// </summary>
    private void EmitHotplugEvent(libusb_hotplug_event eventType, UsbDeviceDescriptor descriptor)
    {
        if (_listener is not { } listener)
        {
            return;
        }
        Action<IUsbDeviceDescriptor> callback =
            eventType == libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
                ? listener.OnDeviceArrived
                : listener.OnDeviceLeft;
        // Always called under _dispatchLock, so only the owning thread can nest
        var previousDispatchThreadId = _dispatchThreadId;
        Volatile.Write(ref _dispatchThreadId, Environment.CurrentManagedThreadId);
        try
        {
            EventDispatch.RaiseSafely(callback, _logger, descriptor, descriptor.DeviceKey);
        }
        finally
        {
            Volatile.Write(ref _dispatchThreadId, previousDispatchThreadId);
        }
    }
}
