namespace UsbDotNet.Descriptor;

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
