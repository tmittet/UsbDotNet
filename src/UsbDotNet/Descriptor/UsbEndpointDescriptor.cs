namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public record struct UsbEndpointDescriptor(
    UsbEndpointAddress EndpointAddress,
    UsbEndpointAttributes Attributes,
    ushort MaxPacketSize,
    byte Interval,
    byte Refresh,
    byte SynchAddress,
    byte[] ExtraBytes
) : IUsbEndpointDescriptor;
