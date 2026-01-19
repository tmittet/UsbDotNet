using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Tests.TestInfrastructure;

internal static class SafeDeviceListExtension
{
    /// <summary>
    /// Returns the first device, or throws SkipException when no device are found.
    /// </summary>
    /// <exception cref="SkipException">Thrown when no device is found.</exception>
    public static ISafeDevice GetAnyDeviceOrSkipTest(this ISafeDeviceList deviceList)
    {
        return deviceList.Count > 0
            ? deviceList[0]
            : throw new SkipException("No USB device found.");
    }

    /// <summary>
    /// Returns a device that can be opened and has a readable serial, or throws SkipException.
    /// </summary>
    /// <exception cref="SkipException">Thrown when no accessible device is found.</exception>
    public static ISafeDevice GetAccessibleDeviceOrSkipTest(this ISafeDeviceList deviceList)
    {
        var device = deviceList.FirstOrDefault(IsAccessibleDevice);
        return device ?? throw new SkipException("No accessible USB device available.");
    }

    private static bool IsAccessibleDevice(ISafeDevice device)
    {
        try
        {
            using var handle = device.Open();
            return IsSerialNumberReadable(handle);
        }
        catch (LibUsbException ex)
            when (ex.Error
                    is libusb_error.LIBUSB_ERROR_ACCESS
                        or libusb_error.LIBUSB_ERROR_IO
                        or libusb_error.LIBUSB_ERROR_NO_DEVICE
                        or libusb_error.LIBUSB_ERROR_NOT_SUPPORTED
            )
        {
            return false;
        }
    }

    private static bool IsSerialNumberReadable(ISafeDeviceHandle handle)
    {
        try
        {
            var serialNumber = handle.GetStringDescriptorAscii(
                handle.Device.GetDeviceDescriptor().iSerialNumber
            );
            return !string.IsNullOrEmpty(serialNumber);
        }
        catch (LibUsbException ex) when (ex.Error is libusb_error.LIBUSB_ERROR_INVALID_PARAM)
        {
            return false;
        }
    }
}
