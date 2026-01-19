namespace UsbDotNet.Transfer;

public enum ControlRequestRecipient : byte
{
    /// <summary>
    /// Targets the whole device.
    /// </summary>
    Device = 0,

    /// <summary>
    /// Targets a specific interface.
    /// </summary>
    Interface = 1,

    /// <summary>
    /// Targets a specific endpoint.
    /// </summary>
    Endpoint = 2,

    /// <summary>
    /// Targets "other" elements defined by a class spec (not an interface or endpoint).
    /// </summary>
    Other = 3,
}
