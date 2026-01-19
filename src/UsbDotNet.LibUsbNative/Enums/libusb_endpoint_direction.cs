using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>Endpoint direction. Values for bit 7 of the endpoint address scheme.</summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_endpoint_direction>))]
#endif
public enum libusb_endpoint_direction : byte
{
    /// <summary>Out: host-to-device.</summary>
    LIBUSB_ENDPOINT_OUT = 0x00,

    /// <summary>In: device-to-host.</summary>
    LIBUSB_ENDPOINT_IN = 0x80,
}
