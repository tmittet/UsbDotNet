using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Device filter used when enumerating devices and when subscribing to hotplug events.
/// </summary>
public interface IUsbDeviceFilter
{
    /// <summary>Returns whether the given descriptor satisfies this filter.</summary>
    bool Matches(IUsbDeviceDescriptor descriptor);

    /// <summary>
    /// Returns a string representation of the filter.
    /// </summary>
    string ToString();
}
