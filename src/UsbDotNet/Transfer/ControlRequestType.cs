namespace UsbDotNet.Transfer;

/// <summary>
/// Specifies the type of USB control request being issued.
/// </summary>
/// <remarks>
/// This enumeration identifies whether a control request is defined by the USB standard, a specific
/// USB class, or a device vendor. Use this value to determine how to interpret or construct control
/// requests when communicating with USB devices.
/// </remarks>
public enum ControlRequestType : byte
{
    /// <summary>
    /// Value per the standard control requests defined in the USB specification.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Values defined in the individual USB class specification.
    /// </summary>
    Class = 1,

    /// <summary>
    /// Values defined by device vendor.
    /// </summary>
    Vendor = 2,
}
