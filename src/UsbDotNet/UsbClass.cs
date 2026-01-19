namespace UsbDotNet;

/// <summary>
/// Device and/or interface class codes. See:
/// https://www.usb.org/defined-class-codes
/// https://libusb.sourceforge.io/api-1.0/group__libusb__desc.html#gac529888effbefef8af4f9d17ebc903a1
/// </summary>
public enum UsbClass : byte
{
    /// <summary>
    /// This base class value indicates that each interface specifies its own class information
    /// and all interfaces operate independently. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    PerInterface = 0x00,

    /// <summary>
    /// This base class is defined for Audio capable devices that conform to the Audio Device
    /// Class Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    Audio = 0x01,

    /// <summary>
    /// This base class is defined for devices that conform to the Communications Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    Communications = 0x02,

    /// <summary>
    /// This base class is defined for devices that conform to the Human Interface Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    HID = 0x03,

    /// <summary>
    /// This base class is defined for devices that conform to the Physical Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    Physical = 0x05,

    /// <summary>
    /// This base class is defined for devices that conform to the Still Imaging Device Class
    /// Specification found on the USB-IF website. SubClass: 0x01. Protocol: 0x01.
    /// </summary>
    Image = 0x06,

    /// <summary>
    /// This base class is defined for devices that conform to the Printer Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    Printer = 0x07,

    /// <summary>
    /// This base class is defined for devices that conform to the Mass Storage Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    MassStorage = 0x08,

    /// <summary>
    /// This base class is defined for devices that are USB hubs and conform to the definition
    /// in the USB specification. SubClass: 0x00. Protocol: 0x00, 0x01, 0x02.
    /// </summary>
    Hub = 0x09,

    /// <summary>
    /// This base class is defined for devices that conform to the Communications Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    CDCData = 0x0A,

    /// <summary>
    /// This base class is defined for devices that conform to the Smart Card Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    SmartCard = 0x0B,

    /// <summary>
    /// This base class is defined for devices that conform to the Content Security Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    ContentSecurity = 0x0D,

    /// <summary>
    /// This base class is defined for devices that conform to the Video Device Class Specification
    /// found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    Video = 0x0E,

    /// <summary>
    /// This base class is defined for devices that conform to the Personal Healthcare Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00-0xFF. Protocol: 0x00-0xFF.
    /// </summary>
    PersonalHealthcare = 0x0F,

    /// <summary>
    /// The USB Audio/Video (AV) Device Class Definition describes the methods used to communicate
    /// with devices or functions embedded in composite devices that are used to manipulate audio,
    /// video, voice, and all image- and sound-related functionality.
    /// SubClass: 0x01, 0x02, 0x03. Protocol: 0x00.
    /// </summary>
    AudioVideo = 0x10,

    /// <summary>
    /// This base class is defined for devices that conform to the Billboard Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    Billboard = 0x11,

    /// <summary>
    /// This base class is defined for devices that conform to the USB Type-C Bridge Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    TypeCBridge = 0x12,

    /// <summary>
    /// This base class is defined for devices that conform to the "VESA USB BDP Device"
    /// Specification found at the VESA website. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    UsbBulkDisplayProtocol = 0x13,

    /// <summary>
    /// This base class is defined for devices that conform to the "MCTP over USB" Specification
    /// found at the DMTF website as DSP0283. SubClass: 0x00, 0x01. Protocol: 0x01, 0x02.
    /// </summary>
    MctpOverUsb = 0x14,

    /// <summary>
    /// This base class is defined for devices that conform to the USB I3C Device Class
    /// Specification found on the USB-IF website. SubClass: 0x00. Protocol: 0x00.
    /// </summary>
    I3CDevice = 0x3C,

    /// <summary>
    /// This base class is defined for devices that provide diagnostic functionality. This class
    /// code can be used in Device or Interface descriptors. Trace is a form of debugging where
    /// processor or system activity is made externally visible in real-time or stored and later
    /// retrieved for viewing by an application developer, program or external equipment for
    /// observing system activity. Design for Debug or Test (DfX). This refers to a logic block that
    /// provides debug or test support (E.g. via Test Access Port (TAP)). DvC: Debug Capability on
    /// the USB device.
    /// SubClass: 0x01-0x08. Protocol: 0x00, 0x01.
    /// </summary>
    Diagnostic = 0xDC,

    /// <summary>
    /// This base class is defined for devices that are wireless controllers.
    /// SubClass: 0x01, 0x02. Protocol: 0x01, 0x02, 0x03, 0x04.
    /// </summary>
    WirelessController = 0xE0,

    /// <summary>
    /// This base class is defined for miscellaneous device definitions.
    /// SubClass: 0x01, 0x02, 0x03, 0x04. Protocol: 0x00-0x07.
    /// </summary>
    Miscellaneous = 0xEF,

    /// <summary>
    /// This base class is defined for devices that conform to several class specifications found
    /// on the USB-IF website. SubClass: 0x01, 0x02, 0x03, 0x04. Protocol: 0x00, 0x01.
    /// </summary>
    ApplicationSpecific = 0xFE,

    /// <summary>
    /// This base class is defined for vendors to use as they please.
    /// These class codes can be used in both Device and Interface Descriptors.
    /// </summary>
    VendorSpecific = 0xFF,
}
