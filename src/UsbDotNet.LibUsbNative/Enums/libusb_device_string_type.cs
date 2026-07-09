#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Specifies a device string descriptor to retrieve from the host OS.
/// Strings are read by calling <see cref="ILibUsbApi.libusb_get_device_string"/>.
/// </summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_device_string_type>))]
#endif
public enum libusb_device_string_type : int
{
    /// <summary>The manufacturer string descriptor type.</summary>
    LIBUSB_DEVICE_STRING_MANUFACTURER = 0x00,

    /// <summary>The product name string descriptor type.</summary>
    LIBUSB_DEVICE_STRING_PRODUCT,

    /// <summary>The serial number string descriptor type.</summary>
    LIBUSB_DEVICE_STRING_SERIAL_NUMBER,

    /// <summary>The total number of string types.</summary>
    LIBUSB_DEVICE_STRING_COUNT,
}
