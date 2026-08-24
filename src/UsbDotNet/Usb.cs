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

namespace UsbDotNet;

/// <inheritdoc/>
public sealed class Usb : IUsb, IUsbInternal
{
    private static int _instances;

    private readonly object _lock = new();

    private readonly HotplugProvider _hotplug;
    private readonly ILibUsb _libUsb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Usb> _logger;
    private readonly UsbDotNetOptions _options;
    private readonly ConcurrentDictionary<string, UsbDevice> _openDevices = new();

#pragma warning disable CA2213 // Disposable fields should be disposed
    // CA2213 false positive
    private ISafeContext? _context;
#pragma warning restore CA2213 // Disposable fields should be disposed
    private LibUsbEventLoop? _eventLoop;
    private DisposeState _disposeState;

    internal IHotplugProvider HotplugProvider => _hotplug;

    /// <inheritdoc/>
    bool IUsbInternal.IsHotplugSupported =>
        _libUsb.HasCapability(libusb_capability.LIBUSB_CAP_HAS_HOTPLUG);

    /// <inheritdoc/>
    T IUsbInternal.WithInitializedContext<T>(Func<ISafeContext, T> action)
    {
        lock (_lock)
        {
            CheckDisposed();
            return action(GetInitializedContextOrThrow());
        }
    }

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
            _hotplug = new HotplugProvider(this, _loggerFactory.CreateLogger<HotplugProvider>());
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
    ) =>
        _hotplug.RegisterHotplugWithoutListener(deviceClass, vendorId, productId) switch
        {
            HotplugRegistrationResult.Success => true,
            HotplugRegistrationResult.NotSupported => false,
            HotplugRegistrationResult.AlreadyRegistered => true,
        };

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
                    .Where(d => filter.Matches(d) && d.HasValidBcdUsb())
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
                var descriptor = UsbDeviceDescriptor.FromDevice(device);
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
        try
        {
            var successful = listDevice.TryGetDeviceString(stringType, out value, out var error);
            if (successful)
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
                    error!.Value.GetMessage()
                );
            }
        }
        catch (EntryPointNotFoundException ex)
        {
            var libUsbVersion = GetVersion();
            if (libUsbVersion < new Version(1, 0, 30))
            {
                _logger.LogDebug(
                    "Unable to get {StringType} for device '{DeviceKey}' from the operating system "
                        + "via libusb v{LibUsbVersion}; v1.0.30 or later is required. "
                        + "Falling back to device read.",
                    stringType,
                    deviceKey,
                    libUsbVersion
                );
            }
            else
            {
                _logger.LogWarning(
                    "Unable to get {StringType} for device '{DeviceKey}' from the operating system "
                        + "via libusb v{LibUsbVersion}. {ErrorMessage}. Falling back to device read.",
                    stringType,
                    deviceKey,
                    libUsbVersion,
                    ex.Message
                );
            }
        }
        value = null;
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
        // NOTE: Dispose must never run inside a hotplug dispatch. It would deadlock on either
        // dispatch source: live events run on the libusb event-loop thread, which teardown joins
        // (step 4), and the LIBUSB_HOTPLUG_ENUMERATE replay runs on the registering thread while
        // holding a rundown token that Shutdown (step 2) waits out. No guard is needed because no
        // user code can execute inside a dispatch: IHotplugListener is internal, its only
        // implementation (UsbHotplugMonitor) only writes to channels whose readers resume on the
        // thread pool, and transfer completions merely signal an event on the submitting thread.
        //
        // Reintroduce a guard covering BOTH dispatch sources if that ever changes; e.g. a public
        // listener API or hotplug implementation with synchronous continuations.

        UsbDevice[] openDevices;

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

            Debug.Assert(
                Environment.CurrentManagedThreadId != _eventLoop?.ManagedThreadId,
                "Dispose invoked from within a hotplug event handler."
            );

            _disposeState = DisposeState.Disposing;

            openDevices = [.. _openDevices.Values];
        }
        try
        {
            // 2. Deregister hotplug callback, wait for in-flight hotplug callbacks to finish
            //    and release cached hotplug device references
            _hotplug.Shutdown();
            // 3. Dispose devices and cancel transfers
            foreach (var device in openDevices)
            {
                _logger.LogDebug(
                    "Auto disposing device '{DeviceKey}' on Usb type dispose.",
                    device.Descriptor.DeviceKey
                );
                device.Dispose();
            }
            // 4. Stop and join libusb event loop
            _eventLoop?.Dispose();
            _eventLoop = null;
            // 5. Dispose SafeContext (libusb_exit)
            if (_context is not null)
            {
                _context.Dispose();
                Debug.Assert(_context.IsClosed, "SafeContext was not closed after Usb.Dispose().");
                if (!_context.IsClosed)
                {
                    _logger.LogWarning("SafeContext remained referenced after Usb.Dispose().");
                }
                _context = null;
            }
        }
        finally
        {
            lock (_lock)
            {
                LibUsbLogHandler.ClearLogger();
                _ = Interlocked.Exchange(ref _instances, 0);
                _disposeState = DisposeState.Disposed;
                Monitor.PulseAll(_lock);
            }
            _hotplug.NotifyProviderDisposed();
        }
    }
}
