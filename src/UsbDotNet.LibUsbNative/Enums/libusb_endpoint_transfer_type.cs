#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>Endpoint transfer type.</summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_endpoint_transfer_type>))]
#endif
public enum libusb_endpoint_transfer_type : byte
{
    /// <summary>Control endpoint.</summary>
    LIBUSB_ENDPOINT_TRANSFER_TYPE_CONTROL = 0,

    /// <summary>Isochronous endpoint.</summary>
    LIBUSB_ENDPOINT_TRANSFER_TYPE_ISOCHRONOUS = 1,

    /// <summary>Bulk endpoint.</summary>
    LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK = 2,

    /// <summary>Interrupt endpoint.</summary>
    LIBUSB_ENDPOINT_TRANSFER_TYPE_INTERRUPT = 3,
}
