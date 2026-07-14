namespace UsbDotNet.Hotplug;

/// <summary>
/// Monitors USB device attach and detach events and fans them out to one or more channel-based
/// subscribers. This decouples event consumers from the internal libusb event loop thread, and
/// allows multiple subscribers each with its own <see cref="UsbDeviceFilter"/>.
/// <para>
/// Create a single monitor per <see cref="IUsb"/> instance and add subscribers; do not create
/// multiple monitors over the same <see cref="IUsb"/>. Hotplug can only be registered once per
/// <see cref="IUsb"/>, so a second monitor throws from its first <see cref="Subscribe"/>.
/// </para>
/// </summary>
public interface IUsbHotplugMonitor : IDisposable
{
    /// <summary>
    /// Creates a subscription that receives hotplug events matching <paramref name="filter"/>.
    /// The new subscription immediately receives a <see cref="UsbHotplugEventType.Connected"/>
    /// event for every matching currently connected device, followed by live events.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the monitor is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the underlying <see cref="IUsb"/> is not initialized or has been disposed, or
    /// when hotplug is already registered on it (for example by another
    /// <see cref="IUsbHotplugMonitor"/>).
    /// </exception>
    IUsbHotplugSubscription Subscribe(IUsbDeviceFilter? filter = null);
}
