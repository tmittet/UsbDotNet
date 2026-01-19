namespace UsbDotNet.Descriptor;

/// <summary>
/// Synchronization type for isochronous endpoints.
/// Values for bits 2-3 of the UsbEndpointAttributes.RawValue field.
/// </summary>
public enum UsbSynchronizationType
{
    None = 0,
    Asynchronous = 1,
    Adaptive = 2,
    Synchronous = 3,
}
