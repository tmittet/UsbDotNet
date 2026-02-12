namespace UsbDotNet.Descriptor;

/// <summary>
/// USB endpoint number.
/// <para>
/// The endpoint number is a 4-bit value in the USB specification;
/// it can only represent values from 0 to 15.
/// </para>
/// </summary>
public enum UsbEndpointNumber : byte
{
    Ep00 = 0,
    Ep01,
    Ep02,
    Ep03,
    Ep04,
    Ep05,
    Ep06,
    Ep07,
    Ep08,
    Ep09,
    Ep10,
    Ep11,
    Ep12,
    Ep13,
    Ep14,
    Ep15,
}
