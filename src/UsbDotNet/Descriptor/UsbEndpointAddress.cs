namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents a USB endpoint address, including its direction and endpoint number,
/// as specified in a USB endpoint descriptor.
/// </summary>
/// <remarks>
/// The USB endpoint address is an 8-bit value defined by the USB specification, where the lower
/// four bits indicate the endpoint number and the most significant bit indicates the direction.
/// This type provides convenient access to these components. Two addresses are equal when their
/// raw values are equal.
/// </remarks>
/// <param name="RawValue">Raw value from USB endpoint descriptor.</param>
public readonly record struct UsbEndpointAddress(byte RawValue)
{
    /// <summary>
    /// Bits 0:3 of the RawValue are the endpoint number.
    /// </summary>
    public UsbEndpointNumber Number => (UsbEndpointNumber)(RawValue & 0x0F);

    /// <summary>
    /// Bit 7 of the RawValue indicates direction; output: 0x00-0x7F, input: 0x80-0xFF.
    /// </summary>
    public UsbEndpointDirection Direction =>
        (RawValue & 0x80) != 0 ? UsbEndpointDirection.Input : UsbEndpointDirection.Output;

    /// <inheritdoc/>
    public override string ToString() => $"{Direction} {Number} (0x{RawValue:X2})";
}
