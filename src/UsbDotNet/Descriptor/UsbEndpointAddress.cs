namespace UsbDotNet.Descriptor;

public class UsbEndpointAddress
{
    /// <summary>
    /// Bits 0:3 of the RawValue are the endpoint number.
    /// </summary>
    public UsbEndpointNumber Number { get; }

    /// <summary>
    /// Bit 7 of the RawValue indicates direction; output: 0x00-0x7F, input: 0x80-0xFF.
    /// </summary>
    public UsbEndpointDirection Direction { get; }

    /// <summary>
    /// Raw value from USB endpoint descriptor.
    /// </summary>
    public byte RawValue { get; }

    public UsbEndpointAddress(byte rawValue)
    {
        RawValue = rawValue;
        Direction =
            (rawValue & 0x80) != 0 ? UsbEndpointDirection.Input : UsbEndpointDirection.Output;
        Number = (UsbEndpointNumber)(rawValue & 0x0F);
    }

    public override string ToString() => $"{Direction} {Number} (0x{RawValue:X2})";
}
