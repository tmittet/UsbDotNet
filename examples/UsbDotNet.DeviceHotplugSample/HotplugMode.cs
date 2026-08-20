namespace UsbDotNet.DeviceHotplugSample;

/// <summary>How the sample consumes <see cref="Hotplug.IUsbHotplugMonitor"/>.</summary>
internal enum HotplugMode
{
    /// <summary>Enumerate the subscription directly with <c>await foreach</c>.</summary>
    Stream,

    /// <summary>Attach classic .NET event handlers via <c>UsbHotplugEventNotifier</c>.</summary>
    Events,

    /// <summary>
    /// Classic .NET event handlers again, but with the notifier owning the subscription: start it
    /// and dispose it, rather than awaiting a run task.
    /// </summary>
    BackgroundEvents,
}
