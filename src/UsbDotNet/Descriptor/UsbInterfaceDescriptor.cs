namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public record struct UsbInterfaceDescriptor(
    byte InterfaceNumber,
    byte AlternateSetting,
    UsbClass InterfaceClass,
    byte InterfaceSubClass,
    byte InterfaceProtocol,
    byte StringDescriptionIndex,
    byte[] ExtraBytes,
    List<IUsbEndpointDescriptor> Endpoints
) : IUsbInterfaceDescriptor
{
    public override readonly string ToString() => $"{InterfaceClass} #{InterfaceNumber}";
}
