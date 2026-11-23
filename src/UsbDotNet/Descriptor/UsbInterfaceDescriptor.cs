namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public readonly record struct UsbInterfaceDescriptor(
    byte InterfaceNumber,
    byte AlternateSetting,
    UsbClass InterfaceClass,
    byte InterfaceSubClass,
    byte InterfaceProtocol,
    byte StringDescriptorIndex,
    ReadOnlyMemory<byte> ExtraBytes,
    IReadOnlyCollection<IUsbEndpointDescriptor> Endpoints
) : IUsbInterfaceDescriptor
{
    /// <inheritdoc/>
    public override readonly string ToString() => $"{InterfaceClass} #{InterfaceNumber}";
}
