namespace UsbDotNet.Transfer;

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
