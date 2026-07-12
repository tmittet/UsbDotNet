namespace UsbDotNet.Hotplug;

/// <summary>
/// The kind of hotplug event carried by a <see cref="UsbHotplugEvent"/>.
/// </summary>
public enum UsbHotplugEventType
{
    /// <summary>A device was connected (or already connected when monitoring started).</summary>
    Connected,

    /// <summary>A device was disconnected.</summary>
    Disconnected,
}
