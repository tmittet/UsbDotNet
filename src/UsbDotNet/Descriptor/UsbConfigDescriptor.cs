namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public record struct UsbConfigDescriptor(
    byte ConfigId,
    byte StringDescriptionIndex,
    UsbConfigAttributes Attributes,
    byte MaxPowerRawValue,
    byte[] ExtraBytes,
    IDictionary<byte, IDictionary<byte, IUsbInterfaceDescriptor>> Interfaces
) : IUsbConfigDescriptor
{
    public readonly int MaxPower => MaxPowerRawValue * 2;
}
