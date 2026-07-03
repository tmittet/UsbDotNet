namespace UsbDotNet.DeviceHotplugSample;

internal enum HotplugMode
{
    /// <summary>Read events from a subscription channel (the default).</summary>
    Channels,

    /// <summary>Receive events via a classic EventHandler adapter.</summary>
    Events,
}
