#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// bmAttributes byte of the USB configuration descriptor.
/// </summary>
[Flags]
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_config_desc_attributes>))]
#endif
public enum libusb_config_desc_attributes : byte
{
    /// <summary>
    /// Bits 4..0 Reserved, set to 0.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// bit 5 Remote Wakeup.
    /// </summary>
    REMOTE_WAKEUP = 0b00100000,

    /// <summary>
    /// Bit 6 Self Powered.
    /// </summary>
    SELF_POWERED = 0b01000000,

    /// <summary>
    /// Bit 7 Reserved, set to 1. (USB 1.0 Bus Powered)
    /// </summary>
    RESERVED_MUST_BE_SET = 0b10000000,
}
