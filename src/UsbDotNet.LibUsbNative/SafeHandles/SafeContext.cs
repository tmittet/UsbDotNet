using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.Functions;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeContext : SafeHandle, ISafeContext
{
    private int _logCallbackRegistered;
    private Action<libusb_log_level, string>? _logHandler;
    private GCHandle? _logCallbackHandle;

    internal ILibUsbApi Api { get; init; }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeContext(ILibUsbApi api)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        var result = api.libusb_init(out var rawHandle);
        if (result != 0 || rawHandle == IntPtr.Zero)
        {
            throw result.ToLibUsbExceptionForApi(nameof(Api.libusb_init));
        }
        Api = api;
        handle = rawHandle;
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        Api.libusb_exit(handle);
        _logCallbackHandle?.Free();
        return true;
    }

    /// <inheritdoc />
    public void SetOption(libusb_option libusbOption, int value)
    {
        SafeHelper.ThrowIfClosed(this);
        var result = Api.libusb_set_option(handle, libusbOption, value);
        result.ThrowLibUsbExceptionForApi(nameof(Api.libusb_set_option));
    }

    /// <inheritdoc />
    public void SetOption(libusb_option libusbOption, nint value)
    {
        SafeHelper.ThrowIfClosed(this);
        var result = Api.libusb_set_option(handle, libusbOption, value);
        result.ThrowLibUsbExceptionForApi(nameof(Api.libusb_set_option));
    }

    /// <inheritdoc />
    public libusb_error HandleEventsCompleted(nint completedPtr)
    {
        SafeHelper.ThrowIfClosed(this);

        return completedPtr == IntPtr.Zero // TODO: Zero pointer OK according to libusb docs.
            ? throw new ArgumentNullException(nameof(completedPtr))
            : Api.libusb_handle_events_completed(handle, completedPtr);
    }

    /// <inheritdoc />
    public void InterruptEventHandler()
    {
        SafeHelper.ThrowIfClosed(this);
        Api.libusb_interrupt_event_handler(handle);
    }

    /// <inheritdoc />
    public void RegisterLogCallback(Action<libusb_log_level, string> logHandler)
    {
        SafeHelper.ThrowIfClosed(this);
        ArgumentNullException.ThrowIfNull(logHandler);

        if (Interlocked.CompareExchange(ref _logCallbackRegistered, 1, 0) != 0)
            throw new InvalidOperationException("Log callback is already registered.");

        _logHandler = logHandler;
        var callback = new libusb_log_cb((_, level, message) => _logHandler(level, message));
        _logCallbackHandle = GCHandle.Alloc(callback);
        SetOption(
            libusb_option.LIBUSB_OPTION_LOG_CB,
            Marshal.GetFunctionPointerForDelegate(callback)
        );
    }

    /// <summary>
    /// Attempt to log a message using the registered log handler; if there is one.
    /// </summary>
    internal void Log(libusb_log_level level, string message)
    {
        _logHandler?.Invoke(level, message);
    }

    /// <inheritdoc />
    public ISafeCallbackHandle RegisterHotplugCallback(
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        Func<ISafeContext, ISafeDevice, libusb_hotplug_event, libusb_hotplug_return> callback,
        libusb_class_code? deviceClass,
        ushort? vendorId,
        ushort? productId
    ) =>
        RegisterHotplugCallback(
            events,
            flags,
            (context, device, eventType, _) => callback(context, device, eventType),
            IntPtr.Zero,
            deviceClass,
            vendorId,
            productId
        );

    /// <inheritdoc />
    public ISafeCallbackHandle RegisterHotplugCallback(
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        Func<ISafeContext, ISafeDevice, libusb_hotplug_event, nint, libusb_hotplug_return> callback,
        nint userData,
        libusb_class_code? deviceClass,
        ushort? vendorId,
        ushort? productId
    )
    {
        const int HotPlugMatchAny = -1;

        SafeHelper.ThrowIfClosed(this);
        ArgumentNullException.ThrowIfNull(callback);

        var safeHandle = new SafeHotplugCallbackHandle(this);
        // Create a hotplug callback
        var hotplugCallback = new libusb_hotplug_callback_fn(
            (_, dev, eventType, userData) =>
                TriggerExternalCallback(safeHandle, dev, eventType, userData, callback)
        );
        // Allocate GCHandle to keep hotplugCallback from being collected
        // We don't need to use GCHandleType.Pinned because it's a delegate,
        // attempting to pin a delegate will throw an exception.
        var gcHandle = GCHandle.Alloc(hotplugCallback, GCHandleType.Normal);
        // Register hotplug hotplugCallback
        var result = Api.libusb_hotplug_register_callback(
            handle,
            events,
            flags,
            vendorId ?? HotPlugMatchAny,
            productId ?? HotPlugMatchAny,
            deviceClass is null ? HotPlugMatchAny : (int)deviceClass,
            hotplugCallback,
            userData,
            out var callbackHandle
        );
        if (result is not libusb_error.LIBUSB_SUCCESS)
        {
            gcHandle.Free();
            safeHandle.Dispose();
        }
        result.ThrowLibUsbExceptionForApi(nameof(Api.libusb_hotplug_register_callback));

        // Increment context reference counter, SafeHotplugCallbackHandle will decrement on dispose
        var success = false;
        DangerousAddRef(ref success);
        if (!success)
        {
            Api.libusb_hotplug_deregister_callback(handle, callbackHandle);
            gcHandle.Free();
            safeHandle.Dispose();
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException("Failed to ref SafeHandle.");
        }

        // Init and return SafeHotplugCallbackHandle that deregister hotplugCallback and decrements ref counter on release
        safeHandle.Initialize(gcHandle, callbackHandle);
        return safeHandle;
    }

    private libusb_hotplug_return TriggerExternalCallback(
        SafeHotplugCallbackHandle callbackHandle,
        nint devicePtr,
        libusb_hotplug_event eventType,
        nint userData,
        Func<ISafeContext, ISafeDevice, libusb_hotplug_event, nint, libusb_hotplug_return> callback
    )
    {
        var success = false;
        // Increment callback ref counter, client that registers callback will dispose and decrement
        callbackHandle.DangerousAddRef(ref success);
        if (!success)
        {
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException(
                "Failed to ref SafeHotplugCallbackHandle."
            );
        }
        // Increment context ref counter, SafeDevice will decrement it on dispose
        DangerousAddRef(ref success);
        if (!success)
        {
            callbackHandle.DangerousRelease();
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException(
                "Failed to ref SafeContext handle."
            );
        }
        try
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            // SafeDevice should be disposed by callback receiver
            return callback(this, new SafeDevice(this, devicePtr), eventType, userData);
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        finally
        {
            callbackHandle.DangerousRelease();
        }
    }

    /// <inheritdoc />
    public ISafeDeviceList GetDeviceList()
    {
        SafeHelper.ThrowIfClosed(this);

        var result = Api.libusb_get_device_list(handle, out var list);
        result.ThrowLibUsbExceptionForApi(nameof(Api.libusb_get_device_list));

        var success = false;
        DangerousAddRef(ref success);
        if (!success)
        {
            Api.libusb_free_device_list(list, 1);
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException("Failed to ref SafeHandle.");
        }

        return new SafeDeviceList(this, list, (int)result);
    }
}
