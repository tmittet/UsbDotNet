namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents a USB endpoint address, including its direction and endpoint number,
/// as specified in a USB endpoint descriptor.
/// </summary>
/// <remarks>
/// The USB endpoint address is an 8-bit value defined by the USB specification, where the lower
/// four bits indicate the endpoint number and the most significant bit indicates the direction.
/// This class provides convenient access to these components.
/// </remarks>
public class UsbEndpointAddress(byte rawValue)
{
    /// <summary>
    /// Bits 0:3 of the RawValue are the endpoint number.
    /// </summary>
    public UsbEndpointNumber Number { get; } = (UsbEndpointNumber)(rawValue & 0x0F);

    /// <summary>
    /// Bit 7 of the RawValue indicates direction; output: 0x00-0x7F, input: 0x80-0xFF.
    /// </summary>
    public UsbEndpointDirection Direction { get; } =
        (rawValue & 0x80) != 0 ? UsbEndpointDirection.Input : UsbEndpointDirection.Output;

    /// <summary>
    /// Raw value from USB endpoint descriptor.
    /// </summary>
    public byte RawValue { get; } = rawValue;

    /// <inheritdoc/>
    public override string ToString() => $"{Direction} {Number} (0x{RawValue:X2})";
}
