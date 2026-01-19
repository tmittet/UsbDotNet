#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Capabilities supported by an instance of libusb on the current running platform.
/// Test if the loaded library supports a given capability by calling libusb_has_capability().
/// </summary>
[Flags]
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_capability>))]
#endif
public enum libusb_capability : uint
{
    NONE = 0,

    /// <summary>
    /// Hotplug support is available on this platform.
    /// </summary>
    LIBUSB_CAP_HAS_HOTPLUG = 0x0001,

    /// <summary>
    /// The library can access HID devices without requiring user intervention.
    /// </summary>
    LIBUSB_CAP_HAS_HID_ACCESS = 0x0100,

    /// <summary>
    /// The library supports detaching of the default USB driver,
    /// using libusb_detach_kernel_driver(), if one is set by the OS kernel.
    /// </summary>
    LIBUSB_CAP_SUPPORTS_DETACH_KERNEL_DRIVER = 0x0101,
}
