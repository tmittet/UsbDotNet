using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Device filter used when enumerating devices and when subscribing to hotplug events.
/// </summary>
/// <param name="VendorId">Optional vendor ID to match.</param>
/// <param name="ProductIds">
/// Optional product IDs; a device matches when its product ID is in the collection.
/// Null matches every product ID; an empty collection matches none.
/// </param>
public sealed record UsbDeviceFilter(
    ushort? VendorId = null,
    IReadOnlyCollection<ushort>? ProductIds = null
)
{
    /// <summary>A filter that matches every device (with a valid descriptor).</summary>
    public static UsbDeviceFilter Any { get; } = new();

    /// <summary>Returns whether the given descriptor satisfies this filter.</summary>
    public bool Matches(IUsbDeviceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return
            // Devices with a synthesized descriptor (BcdUsb == 0) never match, regardless of the
            // filter. The Windows libusb backend produces such descriptors for root hubs and
            // devices with an unreadable descriptor.
            descriptor.BcdUsb != 0
            && (VendorId is null || VendorId == descriptor.VendorId)
            && (ProductIds is null || ProductIds.Contains(descriptor.ProductId));
    }
}
