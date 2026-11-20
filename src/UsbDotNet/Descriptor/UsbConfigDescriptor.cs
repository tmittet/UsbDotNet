namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
/// <remarks>
/// MaxPowerRawValue is the bMaxPower field of the descriptor, in units of 2 mA, or 8 mA when the
/// device operates at SuperSpeed or faster. MaxPower is the same value in milliampere.
/// </remarks>
public readonly record struct UsbConfigDescriptor(
    byte ConfigId,
    byte StringDescriptionIndex,
    UsbConfigAttributes Attributes,
    byte MaxPowerRawValue,
    int MaxPower,
    byte[] ExtraBytes,
    IDictionary<byte, IDictionary<byte, IUsbInterfaceDescriptor>> Interfaces
) : IUsbConfigDescriptor;
