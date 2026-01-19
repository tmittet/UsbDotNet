using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// A callback function must return an int (0 or 1) indicating whether the callback is expecting
/// additional events. See: https://libusb.sourceforge.io/api-1.0/libusb_hotplug.html
/// </summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_hotplug_return>))]
#endif
public enum libusb_hotplug_return
{
    /// <summary>
    /// Rearm the callback.
    /// </summary>
    REARM = 0,

    /// <summary>
    /// Deregister the callback. NOTE: When callbacks are called from
    /// libusb_hotplug_register_callback() the callback return value is ignored.
    /// </summary>
    DEREGISTER = 1,
}
