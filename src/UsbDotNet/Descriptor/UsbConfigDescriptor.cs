namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
/// <remarks>
/// MaxPowerRawValue is the bMaxPower field of the descriptor. MaxPowerMultiplier converts it to
/// milliampere: 2, or 8 when the device operates at SuperSpeed or faster. A multiplier that is
/// not set, as in a descriptor created with an object initializer, is treated as 2.
/// </remarks>
public readonly record struct UsbConfigDescriptor(
    byte ConfigId,
    byte StringDescriptionIndex,
    UsbConfigAttributes Attributes,
    byte MaxPowerRawValue,
    int MaxPowerMultiplier,
    ReadOnlyMemory<byte> ExtraBytes,
    IReadOnlyDictionary<byte, IReadOnlyDictionary<byte, IUsbInterfaceDescriptor>> Interfaces
) : IUsbConfigDescriptor
{
    /// <summary>
    /// The multiplier of a descriptor created without one; the unit below SuperSpeed.
    /// </summary>
    private const int DefaultMaxPowerMultiplier = 2;

    /// <inheritdoc/>
    public readonly int MaxPower =>
        MaxPowerRawValue
        * (MaxPowerMultiplier == 0 ? DefaultMaxPowerMultiplier : MaxPowerMultiplier);
}
