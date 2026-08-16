namespace UsbDotNet.Internal;

internal interface IHotplugProvider
{
    /// <summary>True when hotplug is supported on the platform.</summary>
    bool IsHotplugSupported { get; }

    /// <summary>
    /// Registers the single native hotplug callback and the <paramref name="listener"/> it
    /// notifies. The first successful call registers with enumeration enabled, so every
    /// already-connected device is replayed to <see cref="IHotplugListener.OnDeviceArrived"/>.
    /// <para>
    /// Returns <see cref="HotplugRegistrationResult.Success"/> when the registration is created,
    /// <see cref="HotplugRegistrationResult.AlreadyRegistered"/> while another registration is
    /// active, and <see cref="HotplugRegistrationResult.NotSupported"/> when the platform lacks
    /// hotplug support. The listener is only attached on
    /// <see cref="HotplugRegistrationResult.Success"/>; on any other outcome the provider keeps
    /// its current listener (if any). After <see cref="DeregisterHotplug"/> a new registration
    /// can succeed with a fresh enumeration.
    /// </para>
    /// <para>
    /// <see cref="HotplugRegistrationResult.AlreadyRegistered"/> should be treated as a caller
    /// error: registration is not repeated, so <b>no already-connected devices are enumerated</b>
    /// for the second caller and it will never observe any events. A single caller must own
    /// registration (one registration feeds all subscribers); a second attempt indicates two
    /// components are competing for the same instance.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the instance is not initialized.</exception>
    HotplugRegistrationResult RegisterHotplug(IHotplugListener listener);

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
    void DeregisterHotplug(IHotplugListener listener);
}
