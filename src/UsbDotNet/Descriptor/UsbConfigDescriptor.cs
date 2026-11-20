namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
/// <remarks>
/// MaxPowerRawValue is the bMaxPower field of the descriptor. MaxPowerMultiplier converts it to
/// milliampere: 2, or 8 when the device operates at SuperSpeed or faster.
/// </remarks>
public readonly record struct UsbConfigDescriptor(
    byte ConfigId,
    byte StringDescriptionIndex,
    UsbConfigAttributes Attributes,
    byte MaxPowerRawValue,
    int MaxPowerMultiplier,
    byte[] ExtraBytes,
    IDictionary<byte, IDictionary<byte, IUsbInterfaceDescriptor>> Interfaces
) : IUsbConfigDescriptor
{
    /// <inheritdoc/>
    public readonly int MaxPower => MaxPowerRawValue * MaxPowerMultiplier;
}
