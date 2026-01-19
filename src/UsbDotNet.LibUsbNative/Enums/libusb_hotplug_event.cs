#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Bitwise or of hotplug events that will trigger the callback.
/// </summary>
[Flags]
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_hotplug_event>))]
#endif
public enum libusb_hotplug_event : int
{
    NONE = 0,
    LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED = 0x01,
    LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT = 0x02,
}
