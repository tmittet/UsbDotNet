using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Bitwise or of hotplug flags that affect registration.
/// </summary>
[Flags]
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_hotplug_flag>))]
#endif
public enum libusb_hotplug_flag
{
    NONE = 0,

    /// <summary>
    /// Arm the callback and fire it for all matching currently attached devices.
    /// </summary>
    LIBUSB_HOTPLUG_ENUMERATE = 0x01,
}
