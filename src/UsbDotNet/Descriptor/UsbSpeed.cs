namespace UsbDotNet.Descriptor;

/// <summary>
/// The speed a USB device operates at: the speed negotiated for its connection, which may be
/// lower than what the device is capable of. The values are ordered from slowest to fastest.
/// </summary>
public enum UsbSpeed
{
    /// <summary>The operating system does not report or know the speed of the device.</summary>
    Unknown = 0,

    /// <summary>Low speed (1.5 Mbit/s).</summary>
    Low = 1,

    /// <summary>Full speed (12 Mbit/s).</summary>
    Full = 2,

    /// <summary>High speed (480 Mbit/s).</summary>
    High = 3,

    /// <summary>SuperSpeed (5 Gbit/s).</summary>
    Super = 4,

    /// <summary>SuperSpeed Plus (10 Gbit/s).</summary>
    SuperPlus = 5,

    /// <summary>SuperSpeed Plus x2 (20 Gbit/s).</summary>
    SuperPlusX2 = 6,
}
