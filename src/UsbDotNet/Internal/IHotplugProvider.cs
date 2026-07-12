using UsbDotNet.Descriptor;

namespace UsbDotNet.Internal;

internal interface IHotplugProvider
{
    /// <summary>
    /// Raised on the libusb event loop thread when a device is connected (and once per
    /// already-connected device at registration time). Handlers must not block; exceptions they
    /// throw are caught and logged so they cannot interrupt event handling for other devices.
    /// </summary>
    event EventHandler<IUsbDeviceDescriptor>? DeviceArrived;

    /// <summary>Raised on the libusb event loop thread when a device is disconnected.</summary>
    event EventHandler<IUsbDeviceDescriptor>? DeviceLeft;

    /// <summary>
    /// Raised exactly once, on the disposing thread, after the provider has completed its
    /// teardown (no locks are held and no further hotplug events can be raised). Lets consumers
    /// tracking device state (e.g. UsbHotplugMonitor) stop cleanly instead of serving a stale
    /// snapshot of a provider that no longer exists.
    /// </summary>
    event EventHandler? Disposed;

    /// <summary>True when hotplug is supported on the platform.</summary>
    bool IsHotplugSupported { get; }

    /// <summary>
    /// Registers the single native hotplug callback that drives <see cref="DeviceArrived"/> and
    /// <see cref="DeviceLeft"/>. The first successful call registers with enumeration enabled, so
    /// every already-connected device is replayed as a <see cref="DeviceArrived"/> event.
    /// <para>
    /// Returns <see cref="HotplugRegistrationResult.Success"/> on the first registration and
    /// <see cref="HotplugRegistrationResult.AlreadyRegistered"/> on any subsequent call.
    /// </para>
    /// <para>
    /// <see cref="HotplugRegistrationResult.AlreadyRegistered"/> should be treated as a caller
    /// error: registration is not repeated, so <b>no already-connected devices are enumerated</b>
    /// for the second caller and it will only observe devices that arrive from then on.
    /// A single caller must own registration (one registration feeds all subscribers); a second
    /// attempt indicates two components are competing for the same instance.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the instance is not initialized.</exception>
    HotplugRegistrationResult RegisterHotplug();
}
