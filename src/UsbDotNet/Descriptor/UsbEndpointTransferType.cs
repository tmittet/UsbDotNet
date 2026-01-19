namespace UsbDotNet.Descriptor;

/// <summary>
/// Endpoint transfer type.
/// Values for bits 0-1 of the UsbEndpointAttributes.RawValue field.
/// </summary>
public enum UsbEndpointTransferType
{
    Control = 0,
    Isochronous = 1,
    Bulk = 2,
    Interrupt = 3,
}
