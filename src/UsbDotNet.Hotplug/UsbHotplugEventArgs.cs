using UsbDotNet.Descriptor;

namespace UsbDotNet.Hotplug;

/// <summary>
/// Provides the device descriptor for a hotplug event raised by <see cref="UsbHotplugEventNotifier"/>.
/// </summary>
public sealed class UsbHotplugEventArgs : EventArgs
{
    /// <summary>
    /// The descriptor of the device that was connected or disconnected.
    /// <para>
    /// The descriptor is a snapshot captured when the event was raised; it remains valid and safe
    /// to read after the device has left. It doesn't involve any requests being sent to the device.
    /// </para>
    /// </summary>
    public IUsbDeviceDescriptor Descriptor { get; }

    /// <summary>
    /// Creates a new <see cref="UsbHotplugEventArgs"/> for the given device descriptor.
    /// </summary>
    public UsbHotplugEventArgs(IUsbDeviceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }
}
