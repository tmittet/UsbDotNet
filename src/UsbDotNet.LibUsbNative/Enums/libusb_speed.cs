using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>Indicates the speed at which the device is operating.</summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_speed>))]
#endif
public enum libusb_speed
{
    /// <summary>The OS doesn't report or know the device speed.</summary>
    LIBUSB_SPEED_UNKNOWN = 0,

    /// <summary>The device is operating at low speed (1.5MBit/s).</summary>
    LIBUSB_SPEED_LOW = 1,

    /// <summary>The device is operating at full speed (12MBit/s).</summary>
    LIBUSB_SPEED_FULL = 2,

    /// <summary>The device is operating at high speed (480MBit/s).</summary>
    LIBUSB_SPEED_HIGH = 3,

    /// <summary>The device is operating at super speed (5000MBit/s).</summary>
    LIBUSB_SPEED_SUPER = 4,

    /// <summary>The device is operating at super speed plus (10000MBit/s).</summary>
    LIBUSB_SPEED_SUPER_PLUS = 5,

    /// <summary>The device is operating at super speed plus x2 (20000MBit/s).</summary>
    LIBUSB_SPEED_SUPER_PLUS_X2 = 6,
}
