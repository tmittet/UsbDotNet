using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Device filter used when enumerating devices and when subscribing to hotplug events.
/// </summary>
public interface IUsbDeviceFilter
{
    /// <summary>
    /// Returns whether the given descriptor satisfies this filter.
    /// <para>
    /// The Matches method is called from within the native USB event loop and must be fast and
    /// non-blocking. It should not perform any I/O operations or call into the Usb implementation.
    /// It should only inspect the device descriptor and return true or false.
    /// </para>
    /// </summary>
    bool Matches(IUsbDeviceDescriptor descriptor);

    /// <summary>
    /// Returns a string representation of the filter.
    /// </summary>
    string ToString();
}
