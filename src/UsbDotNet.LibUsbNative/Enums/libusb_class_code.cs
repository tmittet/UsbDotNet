using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>Device and/or Interface Class codes.</summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_class_code>))]
#endif
public enum libusb_class_code : byte
{
    /// <summary>
    /// In the context of a device descriptor, this bDeviceClass value indicates that each
    /// interface specifies its own class information and all interfaces operate independently.
    /// </summary>
    LIBUSB_CLASS_PER_INTERFACE = 0x00,

    /// <summary>Audio class.</summary>
    LIBUSB_CLASS_AUDIO = 0x01,

    /// <summary>Communications class.</summary>
    LIBUSB_CLASS_COMM = 0x02,

    /// <summary>Human Interface Device class.</summary>
    LIBUSB_CLASS_HID = 0x03,

    /// <summary>Physical.</summary>
    LIBUSB_CLASS_PHYSICAL = 0x05,

    /// <summary>Image class.</summary>
    LIBUSB_CLASS_IMAGE = 0x06,

    /// <summary>Printer class.</summary>
    LIBUSB_CLASS_PRINTER = 0x07,

    /// <summary>Mass storage class.</summary>
    LIBUSB_CLASS_MASS_STORAGE = 0x08,

    /// <summary>Hub class.</summary>
    LIBUSB_CLASS_HUB = 0x09,

    /// <summary>Data class.</summary>
    LIBUSB_CLASS_DATA = 0x0A,

    /// <summary>Smart Card.</summary>
    LIBUSB_CLASS_SMART_CARD = 0x0B,

    /// <summary>Content Security.</summary>
    LIBUSB_CLASS_CONTENT_SECURITY = 0x0D,

    /// <summary>Video.</summary>
    LIBUSB_CLASS_VIDEO = 0x0E,

    /// <summary>Personal Healthcare.</summary>
    LIBUSB_CLASS_PERSONAL_HEALTHCARE = 0x0F,

    // TODO: Not defined in libusb
    // TypeCBridge = 0x12,
    // UsbBulkDisplayProtocol = 0x13,
    // MctpOverUsb = 0x14,
    // I3CDevice = 0x3C,

    /// <summary>Diagnostic Device.</summary>
    LIBUSB_CLASS_DIAGNOSTIC_DEVICE = 0xDC,

    /// <summary>Wireless class.</summary>
    LIBUSB_CLASS_WIRELESS = 0xE0,

    /// <summary>Miscellaneous class.</summary>
    LIBUSB_CLASS_MISCELLANEOUS = 0xEF,

    /// <summary>Application class.</summary>
    LIBUSB_CLASS_APPLICATION = 0xFE,

    /// <summary>Class is vendor-specific.</summary>
    LIBUSB_CLASS_VENDOR_SPEC = 0xFF,
}
