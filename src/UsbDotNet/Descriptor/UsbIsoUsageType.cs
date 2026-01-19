namespace UsbDotNet.Descriptor;

/// <summary>
/// Usage type for isochronous endpoints.
/// Values for bits 4-5 of the UsbEndpointAttributes.RawValue field.
/// </summary>
public enum UsbIsoUsageType
{
    /// <summary>
    /// Data endpoint.
    /// </summary>
    Data = 0,

    /// <summary>
    /// Feedback endpoint.
    /// </summary>
    Feedback = 1,

    /// <summary>
    /// Implicit feedback Data endpoint.
    /// </summary>
    ImplicitFeedback = 2,
}
