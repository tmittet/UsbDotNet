using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.Internal;

internal static class UsbDeviceEnum
{
    /// <summary>
    /// Gets a list of USB devices. This does not involve any requests being sent to the devices.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="libusbContext">Pointer to the initialized libusb_init context.</param>
    /// <param name="vendorId">Optional vendor ID filter; only return matching devices.</param>
    /// <param name="productIds">Optional product ID filter; only return matching devices.</param>
    /// <exception cref="ObjectDisposedException">Thrown when context is disposed.</exception>
    /// <exception cref="UsbException">Thrown when the get device list operation fails.</exception>
    internal static List<IUsbDeviceDescriptor> GetDeviceList(
        ILogger logger,
        ISafeContext libusbContext,
        ushort? vendorId,
        HashSet<ushort>? productIds
    )
    {
        using var deviceList = libusbContext.GetDeviceList();

        return GetDeviceDescriptors(logger, deviceList)
            .Select(d => d.Descriptor)
            .Where(d =>
                (vendorId is null || vendorId == d.VendorId)
                && (productIds is null || productIds.Contains(d.ProductId))
            )
            .Cast<IUsbDeviceDescriptor>()
            .ToList();
    }

    /// <summary>
    /// Get cached USB device descriptors for a given, already in memory, device descriptor list.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="devices">Pointer to device list returned by libusb_get_device_list.</param>
    /// <exception cref="ObjectDisposedException">Thrown when device is disposed.</exception>
    internal static List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)> GetDeviceDescriptors(
        ILogger logger,
        IReadOnlyList<ISafeDevice> devices
    )
    {
        var result = new List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)>();
        foreach (var device in devices)
        {
            try
            {
                var descriptor = GetDeviceDescriptor(device);
                if (descriptor.BcdUsb > 0)
                {
                    result.Add((device, descriptor));
                }
            }
            // NOTE: Never throws; since libusb-1.0.16 libusb_get_device_descriptor always succeeds
            catch (UsbException ex)
            {
                logger.LogWarning(ex, "Get device descriptor failed: {ErrorMessage}.", ex.Message);
            }
        }
        return result;
    }

    /// <summary>
    /// Get the cached USB device descriptor for a given, already in memory, device descriptor.
    /// <para>
    /// NOTE: since libusb-1.0.16, LIBUSBX_API_VERSION >= 0x01000102, this function always succeeds.
    /// </para>
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when device is disposed.</exception>
    internal static UsbDeviceDescriptor GetDeviceDescriptor(ISafeDevice device) =>
        new(
            device.GetDeviceDescriptor(),
            device.GetBusNumber(),
            device.GetDeviceAddress(),
            device.GetPortNumber()
        );
}
