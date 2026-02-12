namespace UsbDotNet.Descriptor;

/// <summary>
/// Specifies the direction of data flow for a USB endpoint.
/// </summary>
public enum UsbEndpointDirection
{
    /// <summary>
    /// Data flow: Host -> Device.
    /// </summary>
    Output,

    /// <summary>
    /// Data flow: Device -> Host.
    /// </summary>
    Input,
}
