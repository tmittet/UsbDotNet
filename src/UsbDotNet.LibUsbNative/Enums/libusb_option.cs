using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Available option values for libusb_set_option() and libusb_init_context().
/// </summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_option>))]
#endif
public enum libusb_option
{
    /// <summary>
    /// Set LibUsb log message verbosity. LibUsb recommends LibUsbLogLevel.Warning.
    ///
    /// If the LIBUSB_DEBUG environment variable was set when libusb was initialized,
    /// this option does nothing: the message verbosity is fixed to the value in the
    /// environment variable.
    /// </summary>
    LIBUSB_OPTION_LOG_LEVEL = 0,

    /// <summary>
    /// Use the UsbDk backend for a specific context, if available.
    ///
    /// Only valid on Windows. Ignored on all other platforms.
    /// </summary>
    LIBUSB_OPTION_USE_USBDK = 1,

    /// <summary>
    /// Do not scan for devices during intit.Hotplug functionality will also be deactivated.
    ///
    /// Only valid on Linux. Ignored on all other platforms.
    /// </summary>
    LIBUSB_OPTION_NO_DEVICE_DISCOVERY = 2,

    /// <summary>
    /// Set the context log callback function.
    /// </summary>
    LIBUSB_OPTION_LOG_CB = 3,
}
