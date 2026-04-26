using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet;

/// <inheritdoc/>
public sealed class Usb : IUsb
{
    private static int _instances;

    private readonly object _lock = new();
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
    private ISafeCallbackHandle? _hotplugCallbackHandle;
    private bool _disposed;

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
    /// Creates UsbDotNet with optional LibUsb instance, logging builder, and options.
    /// Consider using DI; registering via IServiceCollection.AddUsbDotNet() instead.
    /// <para>NOTE: Call Initialize() before enumerating or opening devices.</para>
    /// </summary>
    /// <param name="libUsb">
    /// Optional libusb instance. If null, a new default instance will be created.
    /// </param>
    /// <param name="configureLogging">
    /// Optional logging builder; e.g. <c>builder =&gt; builder.AddConsole()</c>.
    /// If null, logging is disabled.
    /// </param>
    /// <param name="configureOptions">
    /// Optional <see cref="UsbDotNetOptions"/> configurator; e.g.
    /// <c>options =&gt; options.NativeLibraryLogLevel = LogLevel.Information</c>.
    /// </param>
    public static Usb Create(
        ILibUsb? libUsb = null,
        Action<ILoggingBuilder>? configureLogging = null,
        Action<UsbDotNetOptions>? configureOptions = null
    )
    {
        var provider = new ServiceCollection()
            .AddLogging(configureLogging ?? (_ => { }))
            .BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var options = new UsbDotNetOptions();
        configureOptions?.Invoke(options);
        return new Usb(libUsb, loggerFactory, loggerFactory.CreateLogger<Usb>(), options);
    }

    /// <summary>
    /// Creates UsbDotNet with all logging disabled.
    /// <para>NOTE: Call Initialize() before enumerating or opening devices.</para>
    /// </summary>
    public Usb()
        : this(
            libUsb: null,
            NullLoggerFactory.Instance,
            NullLogger<Usb>.Instance,
            new UsbDotNetOptions()
        ) { }

    /// <summary>
    /// Creates UsbDotNet with optional LibUsb instance and logging.
    /// <para>NOTE: Call Initialize() before enumerating or opening devices.</para>
    /// </summary>
    /// <param name="libUsb">
    /// Optional libusb instance. If null, a new default instance will be created.
    /// </param>
    /// <param name="loggerFactory">
    /// Logger factory for libusb logging. If null, logging is disabled.
    /// </param>
    [Obsolete(
        "Register via IServiceCollection.AddUsbDotNet() or use Usb.Create(...) for non-DI "
            + "scenarios. This constructor will be removed in a future version."
    )]
    public Usb(ILibUsb? libUsb = default, ILoggerFactory? loggerFactory = null)
        : this(
            libUsb,
            loggerFactory ?? NullLoggerFactory.Instance,
            (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<Usb>(),
            new UsbDotNetOptions()
        ) { }

    internal Usb(
        ILibUsb? libUsb,
        ILoggerFactory loggerFactory,
        ILogger<Usb> logger,
        UsbDotNetOptions options
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
            _loggerFactory = loggerFactory;
            _logger = logger;
            _options = options;
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
        "Configure native LogLevel via UsbDotNetOptions when constructing via Usb.Create or DI,"
            + "and call parameterless Initialize(). This overload will be removed in a future version."
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
    public bool RegisterHotplug(
        UsbClass? deviceClass = default,
        ushort? vendorId = default,
        ushort? productId = default
    )
    {
        var supported = _libUsb.HasCapability(libusb_capability.LIBUSB_CAP_HAS_HOTPLUG);
        if (!supported)
        {
            _logger.LogDebug("Hotplug not supported or unimplemented on this platform.");
            return false;
        }
        lock (_lock)
        {
            CheckDisposed();
            var context = GetInitializedContextOrThrow();
            // We do not follow the recommended libusb init pattern: hotplug first then event loop.
            // See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
            // This should not have any adverse effects as long as we register callback with the
            // LibUsbHotplugFlag.Enumerate flag, as it will allow catching up with current devices.
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
        }
        return true;
    }

    /// <summary>
    /// NOTE:
    /// This callback will run on the LibUsbEventLoop thread. When handling a DeviceArrived event
    /// it's considered safe to call any libusb function that takes a libusb_device. It is also safe
    /// to open a device and submit asynchronous transfers. However, most other functions that take
    /// a libusb_device_handle are not safe to call. Examples of such functions are any of the
    /// synchronous API functions or the blocking functions that retrieve various USB descriptors.
    /// See: https://libusb.sourceforge.io/api-1.0/group__libusb__desc.html
    /// These functions must be used outside of the context of the hotplug callback.
    /// When handling a DeviceLeft event the only safe function is libusb_get_device_descriptor().
    /// </summary>
    private libusb_hotplug_return HotplugEventCallback(
        ISafeContext context,
        ISafeDevice device,
        libusb_hotplug_event eventType
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(device);

        // TODO: Test on macOS and Linux; "most functions that take a device handle are not safe"
        try
        {
            var descriptor = UsbDeviceEnum.GetDeviceDescriptor(device);
            _logger.LogInformation(
                "Hotplug '{EventType}'. Class: {DeviceClass}. Key: {DeviceKey}.",
                eventType,
                descriptor.DeviceClass,
                descriptor.DeviceKey
            );
        }
        // NOTE: Never throws; since libusb-1.0.16 libusb_get_device_descriptor always succeeds
        catch (UsbException ex)
        {
            _logger.LogWarning("Hotplug event handling failed. {ErrorMessage}.", ex.Message);
        }
        device.Dispose();
        return libusb_hotplug_return.REARM;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<IUsbDeviceDescriptor> GetDeviceList(
        ushort? vendorId = default,
        HashSet<ushort>? productIds = default
    )
    {
        lock (_lock)
        {
            CheckDisposed();
            return UsbDeviceEnum.GetDeviceList(
                _logger,
                GetInitializedContextOrThrow(),
                vendorId,
                productIds
            );
        }
    }

    /// <inheritdoc/>
    public string GetDeviceSerial(string deviceKey)
    {
        lock (_lock)
        {
            CheckDisposed();
            if (_openDevices.TryGetValue(deviceKey, out var openDevice))
            {
                return openDevice.GetSerialNumber();
            }
            var context = GetInitializedContextOrThrow();
            using var device = OpenDeviceUnlocked(context, deviceKey);
            return device.GetSerialNumber();
        }
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
        var device = OpenListDeviceUnlocked(context, deviceList, deviceKey);
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

    private UsbDevice OpenListDeviceUnlocked(
        ISafeContext context,
        ISafeDeviceList deviceList,
        string deviceKey
    )
    {
        var (device, descriptor) = UsbDeviceEnum
            .GetDeviceDescriptors(_logger, deviceList)
            .FirstOrDefault(d => d.Descriptor.DeviceKey == deviceKey);
        return device is null
            ? throw new UsbException(
                UsbResult.NotFound,
                "Failed to get device from list; the device could not be found."
            )
            : new UsbDevice(
                _loggerFactory,
                this,
                context,
                device.Open(),
                descriptor,
                device.GetActiveConfigDescriptor().ToUsbConfigDescriptor()
            );
    }

    /// <summary>
    /// Close a USB device. NOTE: Only used internally, called from UsbDevice.Dispose().
    /// </summary>
    internal void CloseDevice(string key, ISafeDeviceHandle handle)
    {
        lock (_lock)
        {
            CheckDisposed();
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

    /// <summary>
    /// Throw ObjectDisposedException when the Usb type is disposed.
    /// </summary>
    private void CheckDisposed()
    {
        if (_disposed)
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
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            if (_context is not null)
            {
                // Disabling hotplug here makes most sense, although done differently in sample code.
                // To ensure event loop exit, libusb_interrupt_event_handler is called on dispose.
                // See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
                // NOTE: Callbacks for a context are automatically deregistered by libusb_exit()
                _hotplugCallbackHandle?.Dispose();
                _eventLoop?.Dispose();
                // Close any devices, interfaces and transfers that remain open or are ongoing
                foreach (var device in _openDevices)
                {
                    _logger.LogDebug(
                        "Auto disposing device '{DeviceKey}' on Usb type dispose.",
                        device.Key
                    );
                    // Device dispose calls Usb.CloseDevice, which removes it from the
                    // _openDevices dictionary. This works without deadlock or race conditions since
                    // the C# Monitor lock is re-entrant and the ConcurrentDictionary is designed to
                    // allow modification during iteration.
                    device.Value.Dispose();
                }

                _context.Dispose();
                Debug.Assert(_context.IsClosed, "SafeContext not closed after dispose.");
                if (!_context.IsClosed)
                {
                    _logger.LogWarning(
                        "Failed to clean up all LibUsb resources. SafeContext not closed after dispose."
                    );
                }
            }
            LibUsbLogHandler.ClearLogger();
            _logger.LogDebug("Usb type disposed.");
            _ = Interlocked.Exchange(ref _instances, 0);
            _disposed = true;
        }
    }
}
