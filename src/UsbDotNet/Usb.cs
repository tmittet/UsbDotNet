using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet;

/// <inheritdoc/>
public sealed class Usb : IUsb, IHotplugProvider
{
    private IHotplugListener? _hotplugListener;

    private static int _instances;

    private readonly object _lock = new();

    // Serializes IHotplugListener invocations across their two dispatch sources: the enumeration
    // replay libusb runs synchronously inside libusb_hotplug_register_callback (on the registering
    // thread) and live events from libusb_handle_events (on the event loop thread). Registration
    // and deregistration acquire it BEFORE _lock, so the global lock order is
    // _hotplugDispatchLock -> _lock; the dispatch path must never take _lock.
    private readonly object _hotplugDispatchLock = new();
    private readonly ILibUsb _libUsb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Usb> _logger;
    private readonly UsbDotNetOptions _options;
    private readonly ConcurrentDictionary<string, UsbDevice> _openDevices = new();

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
    > _hotplugDevices = new();

#pragma warning disable CA2213 // Disposable fields should be disposed
    // CA2213 false positive
    private ISafeContext? _context;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private LibUsbEventLoop? _eventLoop;
    private ISafeCallbackHandle? _hotplugCallbackHandle;
    private readonly RundownGuard _hotplugCallbackRundown = new();
    private DisposeState _disposeState;

    /// <inheritdoc/>
    bool IHotplugProvider.IsHotplugSupported =>
        _libUsb.HasCapability(libusb_capability.LIBUSB_CAP_HAS_HOTPLUG);

    /// <summary>
    /// Get the Usb library version.
    /// </summary>
    public static Version GetVersion()
    {
        var libusb = new LibUsb();
        var version = libusb.GetVersion();
        return new Version(version.major, version.minor, version.micro, version.nano);
    }

    /// <summary>
    /// Creates UsbDotNet with all logging disabled and default options.
    /// <para>NOTE: Call Initialize() before enumerating or opening devices.</para>
    /// </summary>
    public Usb()
        : this(libUsb: null, NullLoggerFactory.Instance, new UsbDotNetOptions()) { }

    /// <summary>
    /// Creates UsbDotNet with optional LibUsb instance, logger factory, and options.
    /// <para>NOTE: Call Initialize() before enumerating or opening devices.</para>
    /// <para>Consider using DI; registered via IServiceCollection.AddUsbDotNet().</para>
    /// </summary>
    /// <param name="libUsb">
    /// Optional libusb instance. If null, a new default instance will be created.
    /// </param>
    /// <param name="loggerFactory">
    /// Optional logger factory. If null, logging is disabled.
    /// </param>
    /// <param name="options">
    /// Optional <see cref="UsbDotNetOptions"/>. If null, default options are used.
    /// </param>
    public Usb(
        ILibUsb? libUsb = default,
        ILoggerFactory? loggerFactory = default,
        UsbDotNetOptions? options = default
    )
    {
        if (Interlocked.CompareExchange(ref _instances, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"Only one instance of the {nameof(Usb)} type allowed."
            );
        }
        try
        {
            _libUsb = libUsb ?? new LibUsb();
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<Usb>();
            _options = options ?? new UsbDotNetOptions();
            LibUsbLogHandler.SetLogger(_logger);
        }
        catch (Exception)
        {
            _ = Interlocked.Exchange(ref _instances, 0);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        lock (_lock)
        {
            CheckDisposed();
            if (_context is not null)
            {
                throw new InvalidOperationException($"{nameof(Usb)} type already initialized.");
            }

            _context = _libUsb.CreateContext();
            _logger.LogInformation("LibUsb v{LibUsbVersion} initialized.", GetVersion());

            InitializeLibUsbLogHandler(_context, _options.NativeLibraryLogLevel);
            _eventLoop = new LibUsbEventLoop(
                _loggerFactory.CreateLogger<LibUsbEventLoop>(),
                _context
            );
            _eventLoop.Start();
        }
    }

    /// <inheritdoc/>
    [Obsolete(
        "Configure native LogLevel via UsbDotNetOptions when using constructor or DI, and "
            + "call parameterless Initialize(). This overload will be removed in a future version."
    )]
    public void Initialize(LogLevel nativeLibraryLogLevel)
    {
        _options.NativeLibraryLogLevel = nativeLibraryLogLevel;
        Initialize();
    }

    private void InitializeLibUsbLogHandler(ISafeContext context, LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return;
        }

        try
        {
            context.RegisterLogCallback((level, message) => LibUsbLogHandler.Log(level, message));
        }
        catch (UsbException ex)
        {
            _logger.LogWarning("Failed to register log callback. {ErrorMessage}.", ex.Message);
            return; // Only attempt to set log level if callback registration succeeded
        }

        var libUsbLogLevel = logLevel.ToLibUsbLogLevel();
        try
        {
            context.SetOption(libusb_option.LIBUSB_OPTION_LOG_LEVEL, (int)libUsbLogLevel);
        }
        catch (UsbException ex)
        {
            _logger.LogWarning("Failed to set LIBUSB_OPTION_LOG_LEVEL: {ErrorMessage}", ex.Message);
        }
    }

    /// <inheritdoc/>
    [Obsolete(
        "Use UsbDotNet.Hotplug package instead. This method will be removed in a future version."
    )]
    public bool RegisterHotplug(
        UsbClass? deviceClass = default,
        ushort? vendorId = default,
        ushort? productId = default
    )
    {
        // Taken before _lock; all registration paths preserve the _hotplugDispatchLock -> _lock
        // order. The enumeration replay re-enters the dispatch lock on this thread from
        // HotplugEventCallback, while live events on the event loop thread wait until the
        // registration (including its replay) completes.
        lock (_hotplugDispatchLock)
        {
            lock (_lock)
            {
                return RegisterHotplugUnlocked(deviceClass, vendorId, productId) switch
                {
                    HotplugRegistrationResult.Success => true,
                    HotplugRegistrationResult.NotSupported => false,
                    HotplugRegistrationResult.AlreadyRegistered => true,
                };
            }
        }
    }

    /// <inheritdoc/>
    HotplugRegistrationResult IHotplugProvider.RegisterHotplug(IHotplugListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        // Taken before _lock; all registration paths preserve the _hotplugDispatchLock -> _lock
        // order. The enumeration replay re-enters the dispatch lock on this thread from
        // HotplugEventCallback, while live events on the event loop thread wait until the
        // registration (including its replay) completes.
        lock (_hotplugDispatchLock)
        {
            lock (_lock)
            {
                // Reject a second listener before touching the slot so a briefly swapped-in
                // listener can never be handed an event that belongs to the current owner.
                if (_hotplugListener is not null)
                {
                    return HotplugRegistrationResult.AlreadyRegistered;
                }
                // Attach before registering: the enumeration replay must reach the listener. The
                // rollback below covers the registration outcomes: AlreadyRegistered from a
                // listenerless legacy RegisterHotplug registration, NotSupported, and throws for
                // uninitialized and disposed.
                _hotplugListener = listener;
                try
                {
                    var result = RegisterHotplugUnlocked(
                        deviceClass: null,
                        vendorId: null,
                        productId: null
                    );
                    if (result is not HotplugRegistrationResult.Success)
                    {
                        _hotplugListener = null;
                    }
                    return result;
                }
                catch
                {
                    _hotplugListener = null;
                    throw;
                }
            }
        }
    }

    /// <inheritdoc/>
    void IHotplugProvider.DeregisterHotplug(IHotplugListener listener)
    {
        // Deliberately no CheckDisposed on the cleanup path (typically UsbHotplugMonitor.Dispose)
        // that may race Usb.Dispose; when Dispose already took the callback handle every step below
        // is a safe no-op.
        ArgumentNullException.ThrowIfNull(listener);
        ISafeCallbackHandle? handle;
        // Holding the dispatch lock makes deregistration a clean cutoff: any in-flight listener
        // invocation holds this lock, so we wait for it, and any dispatch blocked acquiring it
        // observes the cleared listener afterwards and drops the event.
        lock (_hotplugDispatchLock)
        {
            lock (_lock)
            {
                if (!ReferenceEquals(_hotplugListener, listener))
                {
                    // Not the registration owner (or nothing registered); nothing to release.
                    return;
                }
                _hotplugListener = null;
                handle = _hotplugCallbackHandle;
                _hotplugCallbackHandle = null;
            }
            // Native deregister outside _lock, mirroring Dispose.
            handle?.Dispose();
            ReleaseHotplugDeviceCache();
        }
    }

    /// <summary>
    /// Releases the cached hotplug device references (and their libusb refs). Without this, a
    /// later registration's LIBUSB_HOTPLUG_ENUMERATE replay would be suppressed by the
    /// duplicate-arrival check in HandleDeviceArrived, and the cached devices would keep the
    /// native context referenced.
    /// </summary>
    private void ReleaseHotplugDeviceCache()
    {
        foreach (var entry in _hotplugDevices.ToArray())
        {
            if (_hotplugDevices.TryRemove(entry.Key, out var cached))
            {
                cached.Device.Dispose();
            }
        }
    }

    private HotplugRegistrationResult RegisterHotplugUnlocked(
        UsbClass? deviceClass,
        ushort? vendorId,
        ushort? productId
    )
    {
        CheckDisposed();
        var context = GetInitializedContextOrThrow();

        if (!_libUsb.HasCapability(libusb_capability.LIBUSB_CAP_HAS_HOTPLUG))
        {
            _logger.LogDebug("Hotplug not supported or unimplemented on this platform.");
            return HotplugRegistrationResult.NotSupported;
        }
        if (_hotplugCallbackHandle is not null)
        {
            return HotplugRegistrationResult.AlreadyRegistered;
        }
        // We do not follow the recommended libusb init pattern: hotplug first then event
        // loop. See:
        // https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
        // This should not have any adverse effects as long as we register callback with
        // the LibUsbHotplugFlag.Enumerate flag, as it allows catching up with current
        // devices.
        _hotplugCallbackHandle = context.RegisterHotplugCallback(
            libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
                | libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT,
            // Set flag LibUsbHotplugFlag.Enumerate to immediately invoke the
            // HotplugEventCallback method for currently attached devices on register
            libusb_hotplug_flag.LIBUSB_HOTPLUG_ENUMERATE,
            HotplugEventCallback,
            deviceClass is null ? null : (libusb_class_code)deviceClass,
            vendorId,
            productId
        );
        return HotplugRegistrationResult.Success;
    }

    /// <summary>
    /// <para>
    /// NOTE:
    /// This callback runs on the LibUsbEventLoop thread for live events, and on the registering
    /// thread for the LIBUSB_HOTPLUG_ENUMERATE replay; invocations are serialized by
    /// _hotplugDispatchLock.
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
            using var token = _hotplugCallbackRundown.AcquireSharedToken();
            // Serialize the two dispatch sources (enumeration replay on the registering thread,
            // live events on the event loop thread) so the IHotplugListener is never invoked
            // concurrently. Reentrant for the replay, whose registering thread already holds it.
            lock (_hotplugDispatchLock)
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
            descriptor = GetDeviceDescriptor(device);
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
        if (!_hotplugDevices.TryAdd(device.Id, (device, descriptor)))
        {
            // With LIBUSB_HOTPLUG_ENUMERATE libusb may notify the arrival of the same device
            // twice: once from registration enumeration and once from the live event loop.
            _logger.LogDebug(
                "Duplicate hotplug arrival for device '{DeviceKey}' ignored.",
                descriptor.DeviceKey
            );
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
        if (_hotplugDevices.TryRemove(device.Id, out var cached))
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
        // Should not happen; we register with libusb_hotplug_flag.LIBUSB_HOTPLUG_ENUMERATE.
        try
        {
            var descriptor = device.GetDeviceDescriptor();
            _logger.LogDebug(
                "Hotplug 'DEVICE_LEFT' for an untracked device ignored. "
                    + "VID=0x{VendorId:X4}, PID=0x{ProductId:X4}.",
                descriptor.idVendor,
                descriptor.idProduct
            );
        }
        // NOTE: Never throws; since libusb-1.0.16 libusb_get_device_descriptor always succeeds
        catch (UsbException ex)
        {
            _logger.LogError("Hotplug event handling failed. {ErrorMessage}.", ex.Message);
        }
        device.Dispose(); // Dispose the throwaway instance created for the callback
    }

    /// <summary>
    /// NOTE: This method is implemented and expected to never throw.
    /// </summary>
    private void EmitHotplugEvent(libusb_hotplug_event eventType, UsbDeviceDescriptor descriptor)
    {
        _logger.LogDebug(
            "Hotplug '{EventType}'. Class: {DeviceClass}. Key: {DeviceKey}.",
            eventType,
            descriptor.DeviceClass,
            descriptor.DeviceKey
        );
        if (_hotplugListener is not { } listener)
        {
            return;
        }
        Action<IUsbDeviceDescriptor> callback =
            eventType == libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
                ? listener.OnDeviceArrived
                : listener.OnDeviceLeft;
        EventDispatch.RaiseSafely(callback, _logger, descriptor, descriptor.DeviceKey);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IUsbDeviceDescriptor> GetDeviceList(IUsbDeviceFilter? filter = null)
    {
        filter ??= UsbDeviceFilter.Any;
        lock (_lock)
        {
            CheckDisposed();
            var context = GetInitializedContextOrThrow();
            using var deviceList = context.GetDeviceList();
            return
            [
                .. GetDeviceDescriptors(_logger, deviceList)
                    .Select(d => d.Descriptor)
                    .Where(d => filter.Matches(d))
                    .Cast<IUsbDeviceDescriptor>(),
            ];
        }
    }

    /// <summary>
    /// Get cached USB device descriptors for a given, already in memory, device descriptor list.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="devices">Pointer to device list returned by libusb_get_device_list.</param>
    /// <param name="findKey">Return first instance with this key.</param>
    /// <exception cref="ObjectDisposedException">Thrown when device is disposed.</exception>
    private static List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)> GetDeviceDescriptors(
        ILogger logger,
        IReadOnlyList<ISafeDevice> devices,
        string? findKey = null
    )
    {
        var result = new List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)>();
        foreach (var device in devices)
        {
            try
            {
                var descriptor = GetDeviceDescriptor(device);
                if (findKey is null || descriptor.DeviceKey == findKey)
                {
                    result.Add((device, descriptor));
                    if (findKey is not null)
                        break;
                }
            }
            // NOTE: Never throws; since libusb-1.0.16 libusb_get_device_descriptor always succeeds
            catch (UsbException ex)
            {
                logger.LogWarning(ex, "Get device descriptor failed: {ErrorMessage}.", ex.Message);
            }
        }
        return result;
    }

    /// <summary>
    /// Get the cached USB device descriptor for a given, already in memory, device descriptor.
    /// <para>
    /// NOTE: since libusb-1.0.16, LIBUSBX_API_VERSION >= 0x01000102, this function always succeeds.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when device is disposed.</exception>
    private static UsbDeviceDescriptor GetDeviceDescriptor(ISafeDevice device) =>
        new(
            device.GetDeviceDescriptor(),
            device.GetBusNumber(),
            device.GetDeviceAddress(),
            device.GetPortNumber()
        );

    /// <inheritdoc/>
    public string GetDeviceManufacturer(string deviceKey) =>
        GetDeviceString(
            deviceKey,
            libusb_device_string_type.LIBUSB_DEVICE_STRING_MANUFACTURER,
            device => device.GetManufacturer()
        );

    /// <inheritdoc/>
    public string GetDeviceProduct(string deviceKey) =>
        GetDeviceString(
            deviceKey,
            libusb_device_string_type.LIBUSB_DEVICE_STRING_PRODUCT,
            device => device.GetProduct()
        );

    /// <inheritdoc/>
    public string GetDeviceSerial(string deviceKey) =>
        GetDeviceString(
            deviceKey,
            libusb_device_string_type.LIBUSB_DEVICE_STRING_SERIAL_NUMBER,
            device => device.GetSerialNumber()
        );

    private string GetDeviceString(
        string deviceKey,
        libusb_device_string_type stringType,
        Func<UsbDevice, string> readFromDevice
    )
    {
        lock (_lock)
        {
            CheckDisposed();
            var context = GetInitializedContextOrThrow();
            // Attempt to read the value from the operating system
            if (GetOsDeviceString(context, deviceKey, stringType, out var value))
                return value;
            // If the device is already open; read from the open device
            if (_openDevices.TryGetValue(deviceKey, out var openDevice))
                return readFromDevice(openDevice);
            // Open the device to read the value, then close it
            using var device = OpenDeviceUnlocked(context, deviceKey);
            return readFromDevice(device);
        }
    }

    private bool GetOsDeviceString(
        ISafeContext context,
        string deviceKey,
        libusb_device_string_type stringType,
        [NotNullWhen(true)] out string? value
    )
    {
        using var deviceList = context.GetDeviceList();
        (var listDevice, _) = GetListDeviceUnlocked(deviceList, deviceKey);
        if (listDevice.TryGetDeviceString(stringType, out value, out var error))
        {
            if (!string.IsNullOrEmpty(value))
            {
                return true;
            }
            _logger.LogDebug(
                "The {StringType} value read from the operating system "
                    + "for device '{DeviceKey}' is empty. Falling back to device read.",
                stringType,
                deviceKey
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to get {StringType} for device '{DeviceKey}' from the "
                    + "operating system: {ErrorMessage}. Falling back to device read.",
                stringType,
                deviceKey,
                error.Value.GetMessage()
            );
        }
        return false;
    }

    /// <inheritdoc/>
    public IUsbDevice OpenDevice(string deviceKey)
    {
        lock (_lock)
        {
            CheckDisposed();
            if (_openDevices.ContainsKey(deviceKey))
            {
                throw new InvalidOperationException($"Device '{deviceKey}' already open.");
            }
            var context = GetInitializedContextOrThrow();
            return OpenDeviceUnlocked(context, deviceKey);
        }
    }

    private UsbDevice OpenDeviceUnlocked(ISafeContext context, string deviceKey)
    {
        using var deviceList = context.GetDeviceList();
        var (safeDevice, descriptor) = GetListDeviceUnlocked(deviceList, deviceKey);
        var device = new UsbDevice(
            _loggerFactory,
            this,
            context,
            safeDevice.Open(),
            descriptor,
            safeDevice.GetActiveConfigDescriptor().ToUsbConfigDescriptor()
        );
        if (!_openDevices.TryAdd(deviceKey, device))
        {
            device.Dispose();
            throw new UsbException(
                UsbResult.OtherError,
                $"Device with key '{deviceKey}' is already open."
            );
        }
        _logger.LogInformation("UsbDevice '{DeviceKey}' open.", deviceKey);
        return device;
    }

    private (ISafeDevice, UsbDeviceDescriptor) GetListDeviceUnlocked(
        ISafeDeviceList deviceList,
        string deviceKey
    )
    {
        var descriptor = GetDeviceDescriptors(_logger, deviceList, deviceKey).FirstOrDefault();
        return descriptor.device is null
            ? throw new UsbException(
                UsbResult.NotFound,
                "Failed to get device from list; the device could not be found."
            )
            : descriptor;
    }

    /// <summary>
    /// Close a USB device. NOTE: Only used internally, called from UsbDevice.Dispose().
    /// </summary>
    internal void CloseDevice(string key, ISafeDeviceHandle handle)
    {
        lock (_lock)
        {
            // Deliberately not CheckDisposed(): Dispose() closes remaining open devices.
            if (_disposeState is DisposeState.Disposed)
            {
                throw new ObjectDisposedException(nameof(Usb));
            }
            _ = GetInitializedContextOrThrow();
            if (!_openDevices.TryRemove(key, out _))
            {
                throw new InvalidOperationException(
                    $"Device not found in the list of open devices."
                );
            }
            handle.Dispose();
        }
    }

    private void CheckDisposed()
    {
        if (_disposeState is not DisposeState.Live)
        {
            throw new ObjectDisposedException(nameof(Usb));
        }
    }

    /// <summary>
    /// Throw InvalidOperationException when the Usb type is not initialized.
    /// </summary>
    private ISafeContext GetInitializedContextOrThrow() =>
        _context is null ? throw new InvalidOperationException("No context.") : _context;

    /// <summary>
    /// Disposes this Usb context and closes associated devices that remain open. Ongoing
    /// transfers are canceled, any claimed interfaces are released and allocated memory is freed.
    /// </summary>
    public void Dispose()
    {
        UsbDevice[] openDevices;
        ISafeCallbackHandle? hotplugCallbackHandle;
        LibUsbEventLoop? eventLoop;
        ISafeContext? context;
        IHotplugListener? listener = null;

        // 1. Mark Usb as Disposing
        lock (_lock)
        {
            if (_disposeState is DisposeState.Disposed)
            {
                return;
            }
            if (_disposeState is DisposeState.Disposing)
            {
                if (Environment.CurrentManagedThreadId != _eventLoop?.ManagedThreadId)
                {
                    while (_disposeState is not DisposeState.Disposed)
                    {
                        _ = Monitor.Wait(_lock);
                    }
                }
                return;
            }

            if (Environment.CurrentManagedThreadId == _eventLoop?.ManagedThreadId)
            {
                // Thrown if called synchronously from one of the the internal hotplug
                // IHotplugListener.OnDeviceArrived or IHotplugListener.OnDeviceLeft callbacks. The
                // callbacks run on the libusb event-loop thread, and disposing joins that thread.
                const string errorMessage =
                    "Dispose() was invoked from within a hotplug event handler. This is unsafe: "
                    + "hotplug callbacks execute on the libusb event-loop thread, and Dispose() "
                    + "attempts to join that same thread during teardown, causing a deadlock.";
                _logger.LogError(errorMessage);
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
                throw new InvalidOperationException(errorMessage);
#pragma warning restore CA1065
            }
            _disposeState = DisposeState.Disposing;

            hotplugCallbackHandle = _hotplugCallbackHandle;
            _hotplugCallbackHandle = null;
            openDevices = [.. _openDevices.Values];
            eventLoop = _eventLoop;
            context = _context;
        }
        try
        {
            // 2. Deregister hotplug callback
            hotplugCallbackHandle?.Dispose();
            // 3. Wait for any currently executing hotplug callback to finish
            _hotplugCallbackRundown.Dispose();
            // 4. Dispose devices and cancel transfers
            foreach (var device in openDevices)
            {
                _logger.LogDebug(
                    "Auto disposing device '{DeviceKey}' on Usb type dispose.",
                    device.Descriptor.DeviceKey
                );
                device.Dispose();
            }
            // 5. Stop and join libusb event loop
            eventLoop?.Dispose();
            // 6. Release cached hotplug device references
            ReleaseHotplugDeviceCache();
            // 7. Dispose SafeContext (libusb_exit)
            if (context is not null)
            {
                context.Dispose();
                Debug.Assert(context.IsClosed, "SafeContext was not closed after Usb.Dispose().");
                if (!context.IsClosed)
                {
                    _logger.LogWarning("SafeContext remained referenced after Usb.Dispose().");
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _eventLoop = null;
                _context = null;

                LibUsbLogHandler.ClearLogger();
                _ = Interlocked.Exchange(ref _instances, 0);
                _disposeState = DisposeState.Disposed;
                Monitor.PulseAll(_lock);
                listener = _hotplugListener;
            }
            if (listener is not null)
            {
                EventDispatch.RaiseSafely(listener.OnProviderDisposed, _logger);
            }
        }
    }
}
