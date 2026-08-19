using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet;

/// <summary>
/// Internal companion to <see cref="IUsb"/>: gives in-assembly collaborators lock-guarded
/// access to the initialized native context and library capability probes.
/// </summary>
internal interface IUsbInternal
{
    /// <summary>True when hotplug is supported on the platform.</summary>
    bool IsHotplugSupported { get; }

    /// <summary>
    /// Runs <paramref name="action"/> with the initialized <see cref="ISafeContext"/> under
    /// the Usb type's lifetime lock, so the context cannot be disposed while the action runs.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Usb type is not initialized.
    /// </exception>
    T WithInitializedContext<T>(Func<ISafeContext, T> action);
}
