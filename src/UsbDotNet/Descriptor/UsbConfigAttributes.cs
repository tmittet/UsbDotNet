namespace UsbDotNet.Descriptor;

[Flags]
public enum UsbConfigAttributes : byte
{
    None = 0,

    /// <summary>
    /// The configuration supports remote wakeup.
    /// </summary>
    SupportsRemoteWakeup = 0b00100000,

    /// <summary>
    /// The configuration is self-powered and does not use power from the bus.
    /// </summary>
    IsSelfPowered = 0b01000000,
}
