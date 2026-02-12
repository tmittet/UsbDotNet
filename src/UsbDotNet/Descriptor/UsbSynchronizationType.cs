namespace UsbDotNet.Descriptor;

/// <summary>
/// Synchronization type for isochronous endpoints.
/// Values for bits 2-3 of the UsbEndpointAttributes.RawValue field.
/// </summary>
public enum UsbSynchronizationType
{
    /// <summary>
    /// No synchronization. The endpoint does not synchronize data flow.
    /// </summary>
    None = 0,

    /// <summary>
    /// The device does not synchronize data flow; the host may delay
    /// data transmission to avoid overrunning the device's buffer.
    /// </summary>
    Asynchronous = 1,

    /// <summary>
    /// The endpoint provides adaptive data flow. The device adapts the
    /// data flow to the current conditions, such as available bandwidth.
    /// </summary>
    Adaptive = 2,

    /// <summary>
    /// The endpoint provides synchronous data flow.
    /// The device and host synchronize data flow to ensure timely delivery.
    /// </summary>
    Synchronous = 3,
}
