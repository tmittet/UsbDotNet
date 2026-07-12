using UsbDotNet.Descriptor;

namespace UsbDotNet.Hotplug;

/// <summary>
/// A hotplug notification delivered through a subscription channel.
/// </summary>
/// <param name="Type">Whether the device connected or disconnected.</param>
/// <param name="Descriptor">
/// A snapshot of the device descriptor. It remains valid after the
/// device has disconnected and involves no requests to the device.
/// </param>
public readonly record struct UsbHotplugEvent(
    UsbHotplugEventType Type,
    IUsbDeviceDescriptor Descriptor
);
