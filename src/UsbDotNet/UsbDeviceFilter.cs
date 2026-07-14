using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Device filter used when enumerating devices and when subscribing to hotplug events.
/// </summary>
/// <param name="VendorIds">
/// Optional vendor IDs; a device matches when its vendor ID is in the collection.
/// Null matches every vendor ID; an empty collection matches none.
/// </param>
/// <param name="ProductIds">
/// Optional product IDs; a device matches when its product ID is in the collection.
/// Null matches every product ID; an empty collection matches none.
/// </param>
public sealed record UsbDeviceFilter(
    IReadOnlyCollection<ushort>? VendorIds = null,
    IReadOnlyCollection<ushort>? ProductIds = null
) : IUsbDeviceFilter
{
    /// <summary>A filter that matches every device (with a valid descriptor).</summary>
    public static UsbDeviceFilter Any { get; } = new();

    /// <inheritdoc/>
    public bool Matches(IUsbDeviceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return
            // Devices with a synthesized descriptor (BcdUsb == 0) never match, regardless of the
            // filter. The Windows libusb backend produces such descriptors for root hubs and
            // devices with an unreadable descriptor.
            descriptor.BcdUsb != 0
            && (VendorIds is null || VendorIds.Contains(descriptor.VendorId))
            && (ProductIds is null || ProductIds.Contains(descriptor.ProductId));
    }

    /// <summary>
    /// Returns a string representation of the filter, with hex values for vendor and product IDs.
    /// </summary>
    public override string ToString()
    {
        var vids = VendorIds is null ? "*" : string.Join(',', VendorIds.Select(p => $"0x{p:X4}"));
        var pids = ProductIds is null ? "*" : string.Join(',', ProductIds.Select(p => $"0x{p:X4}"));
        return $"UsbDeviceFilter(VendorId='{vids}', ProductIds='{pids}')";
    }
}
