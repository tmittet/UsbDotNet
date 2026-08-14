using UsbDotNet.Descriptor;

namespace UsbDotNet.Internal;

internal interface IHotplugListener
{
    /// <summary>
    /// Invoked on the libusb event loop thread when a device is connected (and once per
    /// already-connected device at registration time). Must not block; exceptions are caught and
    /// logged so they cannot interrupt event handling for other devices.
    /// </summary>
    void OnDeviceArrived(IUsbDeviceDescriptor descriptor);

    /// <summary>Invoked on the libusb event loop thread when a device is disconnected.</summary>
    void OnDeviceLeft(IUsbDeviceDescriptor descriptor);

    /// <summary>
    /// Invoked exactly once, on the disposing thread, after the provider has completed its
    /// teardown (no locks are held and no further hotplug events can be raised). Lets listeners
    /// tracking device state stop cleanly instead of serving a stale snapshot of a provider that
    /// no longer exists.
    /// </summary>
    void OnProviderDisposed();
}
