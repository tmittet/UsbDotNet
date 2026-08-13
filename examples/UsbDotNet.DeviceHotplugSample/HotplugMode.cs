namespace UsbDotNet.DeviceHotplugSample;

/// <summary>How the sample consumes <see cref="Hotplug.IUsbHotplugMonitor"/>.</summary>
internal enum HotplugMode
{
    /// <summary>Enumerate the subscription directly with <c>await foreach</c>.</summary>
    Stream,

    /// <summary>Attach classic .NET event handlers via <c>UsbHotplugEventNotifier</c>.</summary>
    Events,
}
