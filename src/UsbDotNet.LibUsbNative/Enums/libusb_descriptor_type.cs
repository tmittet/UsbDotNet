#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>Descriptor types as defined by the USB specification.</summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_descriptor_type>))]
#endif
public enum libusb_descriptor_type : byte
{
    /// <summary>Device descriptor.</summary>
    LIBUSB_DT_DEVICE = 0x01,

    /// <summary>Configuration descriptor.</summary>
    LIBUSB_DT_CONFIG = 0x02,

    /// <summary>String descriptor.</summary>
    LIBUSB_DT_STRING = 0x03,

    /// <summary>Interface descriptor.</summary>
    LIBUSB_DT_INTERFACE = 0x04,

    /// <summary>Endpoint descriptor.</summary>
    LIBUSB_DT_ENDPOINT = 0x05,

    /// <summary>Interface Association Descriptor.</summary>
    LIBUSB_DT_INTERFACE_ASSOCIATION = 0x0B,

    /// <summary>BOS descriptor.</summary>
    LIBUSB_DT_BOS = 0x0F,

    /// <summary>Device Capability descriptor.</summary>
    LIBUSB_DT_DEVICE_CAPABILITY = 0x10,

    /// <summary>HID descriptor.</summary>
    LIBUSB_DT_HID = 0x21,

    /// <summary>HID report descriptor.</summary>
    LIBUSB_DT_REPORT = 0x22,

    /// <summary>Physical descriptor.</summary>
    LIBUSB_DT_PHYSICAL = 0x23,

    /// <summary>Hub descriptor.</summary>
    LIBUSB_DT_HUB = 0x29,

    /// <summary>SuperSpeed Hub descriptor.</summary>
    LIBUSB_DT_SUPERSPEED_HUB = 0x2A,

    /// <summary>SuperSpeed Endpoint Companion descriptor.</summary>
    LIBUSB_DT_SS_ENDPOINT_COMPANION = 0x30,
}
