using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeContext : IDisposable
{
    /// <summary>
    /// Registers a log callback by calling
    /// <see cref="SetOption(libusb_option, nint)">SetOption(ibusb_option.LIBUSB_OPTION_LOG_CB, function_pointer)</see>.
    /// Only call the RegisterLogCallback method once, consecutive attempts will throw InvalidOperationException.
    ///
    /// NOTE: On osx-arm64 with libusb version 1.0.29 it's not supported and throws LibUsbException with
    /// <see cref="libusb_error.LIBUSB_ERROR_INVALID_PARAM" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when callback is registered more than once.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when log callback registration fails.</exception>
    void RegisterLogCallback(Action<libusb_log_level, string> logHandler);

    /// <summary>
    /// Registers a "hotplug" callback by calling
    /// <see cref="ILibUsbApi.libusb_hotplug_register_callback" />.
    /// </summary>
    /// <returns>A pointer to the handle of the allocated callback (can be zero).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when hotplug callback registration fails.</exception>
    ISafeCallbackHandle RegisterHotplugCallback(
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        Func<ISafeContext, ISafeDevice, libusb_hotplug_event, libusb_hotplug_return> callback,
        libusb_class_code? deviceClass = default,
        ushort? vendorId = default,
        ushort? productId = default
    );

    /// <summary>
    /// Registers a "hotplug" callback by calling
    /// <see cref="ILibUsbApi.libusb_hotplug_register_callback" />.
    /// </summary>
    /// <returns>A pointer to the handle of the allocated callback (can be zero).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when hotplug callback registration fails.</exception>
    ISafeCallbackHandle RegisterHotplugCallback(
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        Func<ISafeContext, ISafeDevice, libusb_hotplug_event, nint, libusb_hotplug_return> callback,
        nint userData,
        libusb_class_code? deviceClass = default,
        ushort? vendorId = default,
        ushort? productId = default
    );

    /// <summary>
    /// Set an option in the library. Use this function to configure a specific option within the
    /// library. Some options require one or more arguments to be provided. Consult each option's
    /// documentation for specific requirements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the option set operation fails.</exception>
    void SetOption(libusb_option libusbOption, int value);

    /// <summary>
    /// Set an option in the library. Use this function to configure a specific option within the
    /// library. Some options require one or more arguments to be provided. Consult each option's
    /// documentation for specific requirements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the option set operation fails.</exception>
    void SetOption(libusb_option libusbOption, nint value);

    /// <summary>
    /// Handle any pending events in blocking mode.
    /// </summary>
    /// <param name="completedPtr">Pointer to completion integer to check.</param>
    /// <returns>
    /// When successful it returns <see cref="libusb_error.LIBUSB_SUCCESS"/>; otherwise it returns the error.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when provided completedPtr is zero.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    libusb_error HandleEventsCompleted(nint completedPtr);

    /// <summary>
    /// Interrupt any active thread that is handling events. This is mainly useful for interrupting
    /// a dedicated event handling thread when an application wishes to call libusb_exit().
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    void InterruptEventHandler();

    /// <summary>
    /// Get an IDisposable SafeDeviceList, that lazyli reads devices when enumerated.
    ///
    /// NOTE: The returned list may throw <see cref="ObjectDisposedException"/>
    /// or <see cref="LibUsbException"/>  during enumeration.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the get device list operation fails.</exception>
    ISafeDeviceList GetDeviceList();

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
