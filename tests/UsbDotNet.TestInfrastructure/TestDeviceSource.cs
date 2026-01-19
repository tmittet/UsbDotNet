using System.Diagnostics.CodeAnalysis;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet.TestInfrastructure;

public sealed class TestDeviceSource(ILogger _logger, IUsb _usb)
{
    private ushort _preferredVendorId;
    private ushort? _requiredVendorId;
    private UsbClass? _interfaceClass;
    private TestDeviceAccess _interfaceAccess = TestDeviceAccess.None;
    private byte? _interfaceSubClass;

    public void SetPreferredVendorId(ushort vendorId)
    {
        _preferredVendorId = vendorId;
    }

    public void SetRequiredVendorId(ushort vendorId)
    {
        _requiredVendorId = vendorId;
    }

    public void SetRequiredInterfaceClass(UsbClass interfaceClass, TestDeviceAccess requiredAccess)
    {
        _interfaceClass = interfaceClass;
        _interfaceAccess = requiredAccess;
    }

    public void SetRequiredInterfaceSubClass(byte interfaceSubClass)
    {
        if (_interfaceClass is null)
        {
            throw new InvalidOperationException(
                "RequiredInterfaceClass must be set before setting RequiredInterfaceSubClass."
            );
        }
        _interfaceSubClass = interfaceSubClass;
    }

    /// <summary>
    /// Returns an open USB device or throws an exception that results
    /// in a "Skipped" result for the test, when no device is found.
    /// </summary>
    public IUsbDevice OpenUsbDeviceOrSkip()
    {
        if (TryOpenUsbDevice(out var openDevice))
            return openDevice;

        throw _interfaceClass is null
            ? new SkipException("No accessible USB device available.")
            : new SkipException($"No suitable {_interfaceClass} interface USB device available.");
    }

    public bool TryOpenUsbDevice([NotNullWhen(true)] out IUsbDevice? openDevice)
    {
        var devices = _usb.GetDeviceList(_requiredVendorId)
            .OrderBy(d => d.VendorId == _preferredVendorId ? 0 : 1);

        foreach (var deviceDescriptor in devices)
        {
            if (TryOpenDevice(deviceDescriptor, out openDevice))
                return true;
        }

        openDevice = null;
        return false;
    }

    public bool TryOpenDevice(
        IUsbDeviceDescriptor deviceDescriptor,
        [NotNullWhen(true)] out IUsbDevice? openDevice,
        int attempts = 3
    )
    {
        IUsbDevice? device = null;
        for (var i = 0; device is null && i < attempts; i++)
        {
            try
            {
                device = _usb.OpenDevice(deviceDescriptor);
            }
            catch (UsbException ex)
                when (ex.Code
                        is UsbResult.AccessDenied
                            or UsbResult.IoError
                            or UsbResult.NotSupported
                )
            {
                if (i > 0)
                    Thread.Sleep(10);
                _logger.LogInformation(
                    "Device '{DeviceKey}' not accessible on attempt #{Attempt}: {ErrorMessage}",
                    deviceDescriptor.DeviceKey,
                    i + 1,
                    ex.Message
                );
            }
        }
        if (
            device is not null
            && DeviceSerialIsReadable(device)
            && (
                _interfaceClass is null
                || DeviceInterfaceIsAccessible(device, _interfaceClass.Value, _interfaceAccess)
            )
            && (
                _interfaceClass is null
                || _interfaceSubClass is null
                || device.HasInterface(_interfaceClass.Value, _interfaceSubClass.Value)
            )
        )
        {
            openDevice = device;
            return true;
        }
        device?.Dispose();
        openDevice = null;
        return false;
    }

    private static bool DeviceInterfaceIsAccessible(
        IUsbDevice device,
        UsbClass interfaceClass,
        TestDeviceAccess interfaceAccess
    )
    {
        var requiresRead = interfaceAccess.HasFlag(TestDeviceAccess.BulkRead);
        var requiresWrite = interfaceAccess.HasFlag(TestDeviceAccess.BulkWrite);
        if (device.HasInterface(interfaceClass))
        {
            try
            {
                if (!requiresRead && !requiresWrite)
                    return true;
                using var usbInterface = device.ClaimInterface(interfaceClass);
                return (!requiresRead || usbInterface.TryGetInputEndpoint(out _))
                    && (!requiresWrite || usbInterface.TryGetOutputEndpoint(out _));
            }
            catch
            {
                // Interface claim failed - device is not accessible
            }
        }
        return false;
    }

    private bool DeviceSerialIsReadable(IUsbDevice device)
    {
        var serialNumberIndex = device.Descriptor.SerialNumberIndex;
        if (serialNumberIndex == 0)
        {
            _logger.LogInformation(
                "Device '{DeviceKey}' serial number index is 0, aka 'no string provided'.",
                device.Descriptor.DeviceKey
            );
            return false;
        }
        try
        {
            var serialNumber = device.ReadStringDescriptor(serialNumberIndex);
            _logger.LogInformation(
                "Device '{DeviceKey}' has serial number '{SerialNumber}'.",
                device.Descriptor.DeviceKey,
                serialNumber
            );
            return true;
        }
        catch (UsbException ex)
        {
            _logger.LogInformation(
                "Device '{DeviceKey}' serial number not readable: {ErrorMessage}",
                device.Descriptor.DeviceKey,
                ex.Message
            );
            return false;
        }
    }
}
