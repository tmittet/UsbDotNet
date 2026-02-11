using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Tests.Usb;

[Trait("Category", "UsbHuddlyVendorClassDevice")]
public sealed class Given_a_supported_Huddly_USB_device : IDisposable
{
    private const ushort HuddlyVendorId = 0x2BD9;
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_supported_Huddly_USB_device> _logger;
    private readonly UsbDotNet.Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_a_supported_Huddly_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_supported_Huddly_USB_device>();
        _usb = new UsbDotNet.Usb(_libusb, _loggerFactory);
        try
        {
            _usb.Initialize(LogLevel.Information);
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetRequiredVendorId(HuddlyVendorId);
            _deviceSource.SetRequiredInterfaceClass(
                UsbClass.VendorSpecific,
                TestDeviceAccess.BulkRead | TestDeviceAccess.BulkWrite
            );
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [SkippableFact]
    public void GetDeviceList_returns_at_least_one_Huddly_USB_device()
    {
        var descriptors = _usb.GetDeviceList(vendorId: HuddlyVendorId);
        Skip.If(descriptors.Count == 0, "No USB device available.");

        descriptors.Should().HaveCountGreaterThanOrEqualTo(1);
        foreach (var descriptor in descriptors)
        {
            _logger.LogInformation(
                "Device found: Class={DeviceClass}, VID=0x{VID:X4}, PID=0x{PID:X4}, "
                    + "BusNumber={BusNumber}, BusAddress={BusAddress}, PortNumber={PortNumber}.",
                descriptor.DeviceClass,
                descriptor.VendorId,
                descriptor.ProductId,
                descriptor.BusNumber,
                descriptor.BusAddress,
                descriptor.PortNumber
            );
        }
    }

    [SkippableFact]
    public void GetDeviceSerial_returns_serial_given_a_Huddly_device_descriptor()
    {
        IUsbDeviceDescriptor deviceDescriptor;
        using (var device = _deviceSource.OpenUsbDeviceOrSkip())
        {
            deviceDescriptor = device.Descriptor;
        }
        var serial = _usb.GetDeviceSerial(deviceDescriptor);
        serial.Should().NotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        _usb.Dispose();
    }
}
