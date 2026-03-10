using UsbDotNet.Extensions.Uvc;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Extensions.Tests.Uvc;

[Trait("Category", "UsbHuddlyVendorClassDevice")]
public sealed class Given_a_supported_Huddly_USB_device_with_UVC : IDisposable
{
    // See: https://github.com/Huddly/XU-API

    private const ushort HuddlyVendorId = 0x2BD9;
    private const byte UvcInterfaceSubClass = UsbDeviceDescriptorExtension.UvcVideoControlSubClass;
    private static readonly Guid ExtensionUnitId = new("f6acc829-acdb-e511-8424-f39068f75511");
    private const byte SoftwareVersionXuControl = 0x13; // 8 bytes ([Byte 3].[Byte 2].[Byte 1])

    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_supported_Huddly_USB_device_with_UVC> _logger;
    private readonly Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_a_supported_Huddly_USB_device_with_UVC(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_supported_Huddly_USB_device_with_UVC>();
        _usb = new Usb(_libusb, _loggerFactory);
        try
        {
            _usb.Initialize(LogLevel.Information);
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetRequiredVendorId(HuddlyVendorId);
            _deviceSource.SetRequiredProductId(0x0021, 0x0031, 0x0041);
            _deviceSource.SetRequiredInterfaceClass(UsbClass.Video, TestDeviceAccess.Control);
            _deviceSource.SetRequiredInterfaceSubClass(UvcInterfaceSubClass);
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [SkippableFact]
    public void It_successfully_reads_device_version()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvcControls = OpenFirstUvcControls(device);

        var readBuffer = new byte[8];
        var bytesRead = uvcControls.GetExtensionUnit(
            ExtensionUnitId,
            SoftwareVersionXuControl,
            readBuffer
        );
        readBuffer[3].Should().Be(1, because: "all IQ devices have major version 1");
        readBuffer[2]
            .Should()
            .BeGreaterThanOrEqualTo(4, because: "all supported IQ devices have minor version >= 4");
        //readBuffer[1].Should().BeGreaterThanOrEqualTo(21);
    }

    private static Descriptor.IUsbInterfaceDescriptor GetFirstUvcInterface(IUsbDevice device) =>
        device.GetInterfaceDescriptorList(UsbClass.Video, UvcInterfaceSubClass).First();

    private static IUvcControls OpenFirstUvcControls(IUsbDevice device)
    {
        var uvcInterface = GetFirstUvcInterface(device);
        return device.OpenUvcControls(uvcInterface.InterfaceNumber);
    }

    public void Dispose()
    {
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
