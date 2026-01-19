using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// Structure providing the version of the libusb runtime.
/// </summary>
public readonly record struct libusb_version
{
    public ushort major { get; }
    public ushort minor { get; }
    public ushort micro { get; }
    public ushort nano { get; }

    /// <summary>
    /// Library release candidate suffix string.
    /// </summary>
    public string rc { get; }

    /// <summary>
    /// For ABI compatibility only.
    /// </summary>
    public string describe { get; }

    [JsonConstructor]
    public libusb_version(
        ushort major,
        ushort minor,
        ushort micro,
        ushort nano,
        string rc,
        string describe
    )
    {
        this.major = major;
        this.minor = minor;
        this.micro = micro;
        this.nano = nano;
        this.rc = rc;
        this.describe = describe;
    }

    public override string ToString()
    {
        var baseVer = $"{major}.{minor}.{micro}.{nano}";
        var rcPart = string.IsNullOrWhiteSpace(rc) ? "" : $" ({rc})";
        var descPart = string.IsNullOrWhiteSpace(describe) ? "" : $" - {describe}";
        return $"libusb {baseVer}{rcPart}{descPart}";
    }
}
