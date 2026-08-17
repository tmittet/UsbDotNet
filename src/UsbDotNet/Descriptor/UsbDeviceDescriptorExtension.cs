namespace UsbDotNet.Descriptor;

internal static class UsbDeviceDescriptorExtension
{
    /// <summary>
    /// Returns true for devices with a valid BcdUsb.
    /// <para>
    /// UsbDotNet treats devices with a synthesized descriptor (BcdUsb == 0) as invalid. Such
    /// devices lack the fields required to form a device key and cannot be opened. The libusb
    /// backend generates these descriptors for root hubs and devices with an unreadable descriptor.
    /// </para>
    /// </summary>
    internal static bool HasValidBcdUsb(this IUsbDeviceDescriptor deviceDescriptor) =>
        deviceDescriptor.BcdUsb != 0;
}
