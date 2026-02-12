namespace UsbDotNet.Descriptor;

/// <summary>
/// Endpoint transfer type.
/// Values for bits 0-1 of the UsbEndpointAttributes.RawValue field.
/// </summary>
public enum UsbEndpointTransferType
{
    /// <summary>
    /// Control transfers are used for configuration, command, and status operations. They are
    /// typically used for device management and control purposes, such as setting device
    /// configurations or retrieving device information.
    /// </summary>
    Control = 0,

    /// <summary>
    /// Isochronous transfers are used for time-sensitive data, such as audio or video streams.
    /// They provide a guaranteed data rate and are suitable for applications that require
    /// continuous data flow.
    /// </summary>
    Isochronous = 1,

    /// <summary>
    /// Bulk transfers are used for large, non-time-sensitive data transfers. They are optimized
    /// for high throughput and can handle variable data sizes.
    /// </summary>
    Bulk = 2,

    /// <summary>
    /// Interrupt transfers are used for low-latency, time-sensitive data, such as input from
    /// a keyboard or mouse. They provide guaranteed delivery of small amounts of data.
    /// </summary>
    Interrupt = 3,
}
