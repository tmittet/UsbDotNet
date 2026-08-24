using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.Internal;

internal static class SafeDeviceExtension
{
    /// <summary>
    /// Get USB device descriptors (from libusb's cached descriptors) for an already-materialized device list.
    /// </summary>
    /// <param name="logger">A logger.</param>
    /// <param name="devices">Device list returned by libusb_get_device_list (via ISafeContext.GetDeviceList()).</param>
    /// <param name="findKey">Return first instance with this key.</param>
    /// <exception cref="ObjectDisposedException">Thrown when device is disposed.</exception>
    internal static List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)> GetDeviceDescriptors(
        this IReadOnlyList<ISafeDevice> devices,
        ILogger logger,
        string? findKey = null
    )
    {
        var result = new List<(ISafeDevice device, UsbDeviceDescriptor Descriptor)>();
        foreach (var device in devices)
        {
            try
            {
                var descriptor = UsbDeviceDescriptor.FromDevice(device);
                if (findKey is null || descriptor.DeviceKey == findKey)
                {
                    result.Add((device, descriptor));
                    if (findKey is not null)
                        break;
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
    /// Get a device from the list by device key, throwing an exception if not found.
    /// </summary>
    internal static (ISafeDevice, UsbDeviceDescriptor) GetListDevice(
        this ISafeDeviceList deviceList,
        ILogger logger,
        string deviceKey
    )
    {
        var descriptor = deviceList.GetDeviceDescriptors(logger, deviceKey).FirstOrDefault();
        return descriptor.device is null
            ? throw new UsbException(
                UsbResult.NotFound,
                "Failed to get device from list; the device could not be found."
            )
            : descriptor;
    }

    /// <summary>
    /// Get a device string from the operating system for a given device key and string type.
    /// Falls back to reading the string from the device if the operating system read fails.
    /// </summary>
    internal static bool GetOsDeviceString(
        this ISafeContext context,
        ILogger logger,
        string deviceKey,
        libusb_device_string_type stringType,
        [NotNullWhen(true)] out string? value
    )
    {
        using var deviceList = context.GetDeviceList();
        (var listDevice, _) = deviceList.GetListDevice(logger, deviceKey);
        try
        {
            var successful = listDevice.TryGetDeviceString(stringType, out value, out var error);
            if (successful)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return true;
                }
                logger.LogDebug(
                    "The {StringType} value read from the operating system "
                        + "for device '{DeviceKey}' is empty. Falling back to device read.",
                    stringType,
                    deviceKey
                );
            }
            else
            {
                logger.LogWarning(
                    "Failed to get {StringType} for device '{DeviceKey}' from the "
                        + "operating system: {ErrorMessage}. Falling back to device read.",
                    stringType,
                    deviceKey,
                    error!.Value.GetMessage()
                );
            }
        }
        catch (EntryPointNotFoundException ex)
        {
            var libUsbVersion = Usb.GetVersion();
            if (libUsbVersion < new Version(1, 0, 30))
            {
                logger.LogDebug(
                    "Unable to get {StringType} for device '{DeviceKey}' from the operating system "
                        + "via libusb v{LibUsbVersion}; v1.0.30 or later is required. "
                        + "Falling back to device read.",
                    stringType,
                    deviceKey,
                    libUsbVersion
                );
            }
            else
            {
                logger.LogWarning(
                    ex,
                    "Unable to get {StringType} for device '{DeviceKey}' from the operating system "
                        + "via libusb v{LibUsbVersion}. Falling back to device read.",
                    stringType,
                    deviceKey,
                    libUsbVersion
                );
            }
        }
        value = null;
        return false;
    }
}
