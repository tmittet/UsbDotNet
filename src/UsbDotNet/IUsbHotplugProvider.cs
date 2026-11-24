namespace UsbDotNet;

/// <summary>
/// Registers the listener that receives USB hotplug notifications; see <see cref="IUsb.Hotplug"/>.
/// <para>
/// This is the low level hotplug API, with a single listener that is invoked on library threads.
/// Most applications should use the UsbDotNet.Hotplug package, which builds device tracking,
/// filtering and any number of subscribers on top of it.
/// </para>
/// </summary>
public interface IUsbHotplugProvider
{
    /// <summary>True when hotplug is supported on the platform.</summary>
    bool IsHotplugSupported { get; }

    /// <summary>
    /// Registers the single native hotplug callback and the <paramref name="listener"/> it
    /// notifies. The first successful call registers with enumeration enabled, so every
    /// already-connected device is replayed to <see cref="IUsbHotplugListener.OnDeviceArrived"/>
    /// before this method returns.
    /// <para>
    /// Returns <see cref="UsbHotplugRegistrationResult.Success"/> when the registration is created,
    /// <see cref="UsbHotplugRegistrationResult.AlreadyRegistered"/> while another registration is
    /// active, and <see cref="UsbHotplugRegistrationResult.NotSupported"/> when the platform lacks
    /// hotplug support. The listener is only attached on
    /// <see cref="UsbHotplugRegistrationResult.Success"/>; on any other outcome the provider keeps
    /// its current listener (if any). After <see cref="DeregisterHotplug"/> a new registration
    /// can succeed with a fresh enumeration.
    /// </para>
    /// <para>
    /// <see cref="UsbHotplugRegistrationResult.AlreadyRegistered"/> should be treated as a caller
    /// error: registration is not repeated, so <b>no already-connected devices are enumerated</b>
    /// for the second caller and it will never observe any events. A single caller must own
    /// registration (one registration feeds all subscribers); a second attempt indicates two
    /// components are competing for the same instance. A hotplug monitor from the
    /// UsbDotNet.Hotplug package owns the registration of the <see cref="IUsb"/> it is created
    /// over.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the instance is not initialized.</exception>
    UsbHotplugRegistrationResult RegisterHotplug(IUsbHotplugListener listener);

    /// <summary>
    /// Releases the hotplug registration owned by <paramref name="listener"/>: deregisters the
    /// native callback, detaches the listener and releases cached device references, so a later
    /// <see cref="RegisterHotplug"/> can succeed with a fresh enumeration.
    /// <para>
    /// A no-op when <paramref name="listener"/> is not the currently attached listener (including
    /// when nothing is registered or the provider is disposed), so disposal paths can call it
    /// unconditionally. Waits for any in-flight notification to complete; once this returns the
    /// listener is never invoked again. Callers must therefore not hold a lock that the listener's
    /// notification handling takes.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attached listener is deregistered from within its own
    /// <see cref="IUsbHotplugListener.OnDeviceArrived"/> or
    /// <see cref="IUsbHotplugListener.OnDeviceLeft"/> notification; the listener stays registered.
    /// </exception>
    void DeregisterHotplug(IUsbHotplugListener listener);
}
