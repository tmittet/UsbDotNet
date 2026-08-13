namespace UsbDotNet.Hotplug;

/// <summary>
/// Monitors USB device attach and detach events and fans them out to one or more subscribers as
/// async streams. This decouples event consumers from the internal libusb event loop thread, and
/// allows multiple subscribers each with its own <see cref="UsbDeviceFilter"/>.
/// <para>
/// Create a single monitor per <see cref="IUsb"/> instance and add subscribers; do not create
/// multiple monitors over the same <see cref="IUsb"/>. Hotplug can only be registered once per
/// <see cref="IUsb"/>, so the first read of a second concurrent monitor's subscription throws.
/// Disposing a monitor releases its registration, so a replacement monitor over the same
/// <see cref="IUsb"/> can subscribe afterwards.
/// </para>
/// </summary>
public interface IUsbHotplugMonitor : IDisposable
{
    /// <summary>
    /// Subscribes to hotplug events matching <paramref name="filter"/> and returns them as an
    /// async stream. The stream begins with a <see cref="UsbHotplugEventType.Connected"/> event
    /// for every matching device connected at the moment enumeration starts, followed by live
    /// events.
    /// <para>
    /// <strong>Nothing happens until you enumerate.</strong> The subscription is created — and the
    /// snapshot of connected devices taken — on the first read, not when this method returns.
    /// Discarding the returned stream therefore costs nothing: no registration, no channel, no
    /// buffered events. Conversely, the exceptions listed below surface on that first read rather
    /// than at this call.
    /// </para>
    /// <para>
    /// Enumeration is what holds the subscription, and disposing the enumerator is what releases
    /// it; <c>await foreach</c> does that for you, including on <c>break</c> and on an exception.
    /// The returned stream is reusable rather than single-use: enumerating it again — or
    /// concurrently — starts an independent subscription with its own snapshot.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the monitor is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the underlying <see cref="IUsb"/> is not initialized or has been disposed, or
    /// when hotplug is already registered on it (for example by another
    /// <see cref="IUsbHotplugMonitor"/>).
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when hotplug is not supported on the platform.
    /// </exception>
    IAsyncEnumerable<UsbHotplugEvent> Subscribe(
        IUsbDeviceFilter? filter = null,
        CancellationToken cancellationToken = default
    );
}
