using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Receives hotplug notifications from an <see cref="IUsbHotplugProvider"/>.
/// <para>
/// Threading: the provider serializes <see cref="OnDeviceArrived"/> and <see cref="OnDeviceLeft"/>
/// invocations, but they may not run on the same thread: live events arrive on the libusb event
/// loop thread, while the replay of already-connected devices arrives on the thread calling
/// <see cref="IUsbHotplugProvider.RegisterHotplug"/>.
/// </para>
/// <para>
/// <see cref="OnDeviceArrived"/> and <see cref="OnDeviceLeft"/> run inside the library's event
/// handling, which sets rules for an implementation:
/// <list type="bullet">
/// <item>
/// It must not block. While it runs no other hotplug event is delivered, and on the event loop
/// thread no transfer completes. Hand the notification over to another thread, and open the
/// device or read from it there.
/// </item>
/// <item>
/// It must not dispose the <see cref="IUsb"/>; doing so throws
/// <see cref="InvalidOperationException"/>.
/// </item>
/// <item>
/// An exception it throws is caught and logged, so that it cannot interrupt the event handling.
/// </item>
/// </list>
/// </para>
/// </summary>
public interface IUsbHotplugListener
{
    /// <summary>
    /// Invoked when a device is connected, and once per already-connected device at registration
    /// time (on the registering thread, see threading note on <see cref="IUsbHotplugListener"/>).
    /// Must not block; exceptions are caught and logged so they cannot interrupt event handling
    /// for other devices.
    /// </summary>
    void OnDeviceArrived(IUsbDeviceDescriptor descriptor);

    /// <summary>
    /// Invoked on the libusb event loop thread when a device is disconnected. Must not block;
    /// exceptions are caught and logged.
    /// </summary>
    void OnDeviceLeft(IUsbDeviceDescriptor descriptor);

    /// <summary>
    /// Invoked exactly once, on the disposing thread, after the provider has completed its
    /// teardown (no locks are held and no further hotplug events can be raised). Lets listeners
    /// tracking device state stop cleanly instead of serving a stale snapshot of a provider that
    /// no longer exists.
    /// </summary>
    void OnProviderDisposed();
}
