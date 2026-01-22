using UsbDotNet.Core;
using UsbDotNet.Extensions.ControlTransfer.Uvc;

namespace UsbDotNet.Extensions.Tests.ControlTransfer.Uvc;

[Trait("Category", "UsbVideoControl")]
public sealed class Given_a_video_class_USB_device_with_UVC : IDisposable
{
    private const byte UvcInterfaceSubClass = 0x01; // SC_VIDEOCONTROL
    private const byte ControlSelector = 0x02;
    private const byte ProcessingUnit = 0x03;

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_video_class_USB_device_with_UVC> _logger;
    private readonly Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_a_video_class_USB_device_with_UVC(ITestOutputHelper output)
    {
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_video_class_USB_device_with_UVC>();
        _usb = new Usb(loggerFactory: _loggerFactory);
        _usb.Initialize(LogLevel.Information);
        _deviceSource = new TestDeviceSource(_logger, _usb);
        _deviceSource.SetPreferredVendorId(0x2BD9);
        _deviceSource.SetRequiredInterfaceClass(UsbClass.Video, TestDeviceAccess.Control);
        _deviceSource.SetRequiredInterfaceSubClass(UvcInterfaceSubClass);
    }

    [SkippableFact]
    public void ControlReadUvc_Brightness_should_complete_successfully()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new SkipException("The UVC control interface is inaccessible on the Windows OS.");
        }
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var serial = device.GetSerialNumber();
        var uvcInterfaces = device.GetInterfaceDescriptorList(UsbClass.Video, UvcInterfaceSubClass);
        var uvcInterface = uvcInterfaces.First();
        _logger.LogInformation(
            "Video device open: VID=0x{VID:X4}, PID=0x{PID:X4}, "
                + "SerialNumber={SerialNumber}, UVC interface: {Interface}.",
            device.Descriptor.VendorId,
            device.Descriptor.ProductId,
            serial,
            uvcInterface.InterfaceNumber
        );
        var readBuffer = new Span<byte>(new byte[2]);
        var result = device.ControlReadUvc(
            readBuffer,
            out var readLength,
            ControlRequestUvc.GetCurrent,
            uvcInterface.InterfaceNumber,
            ProcessingUnit,
            ControlSelector,
            timeout: 500
        );
        var readValue = BitConverter.ToInt16(readBuffer);
        result.Should().Be(UsbResult.Success);
    }

    [SkippableFact]
    public void ControlWriteUvc_Brightness_should_successfully_write_provided_value_to_device()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new SkipException("The UVC control interface is inaccessible on the Windows OS.");
        }
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var serial = device.GetSerialNumber();
        var uvcInterface = device
            .GetInterfaceDescriptorList(UsbClass.Video, UvcInterfaceSubClass)
            .First();
        _logger.LogInformation(
            "Video device open: VID=0x{VID:X4}, PID=0x{PID:X4}, "
                + "SerialNumber={SerialNumber}, UVC interface: {Interface}.",
            device.Descriptor.VendorId,
            device.Descriptor.ProductId,
            serial,
            uvcInterface.InterfaceNumber
        );
        var initialValueBuffer = new Span<byte>(new byte[2]);
        var initialReadResult = device.ControlReadUvc(
            initialValueBuffer,
            out var initialBytesRead,
            ControlRequestUvc.GetCurrent,
            uvcInterface.InterfaceNumber,
            ProcessingUnit,
            ControlSelector,
            timeout: 500
        );
        initialReadResult
            .Should()
            .Be(UsbResult.Success, "The write test can't continue when read is unsuccessful.");

        var initialValue = BitConverter.ToInt16(initialValueBuffer);
        var newValue = (short)(initialValue > 400 ? -600 : initialValue + 150);
        var writeBuffer = new Span<byte>(BitConverter.GetBytes(newValue));
        var writeResult = device.ControlWriteUvc(
            writeBuffer,
            out var bytesWritten,
            ControlRequestUvc.SetCurrent,
            uvcInterface.InterfaceNumber,
            ProcessingUnit,
            ControlSelector,
            timeout: 500
        );
        writeResult.Should().Be(UsbResult.Success);
        bytesWritten.Should().Be(2);

        var newValueBuffer = new Span<byte>(new byte[2]);
        var newReadResult = device.ControlReadUvc(
            newValueBuffer,
            out var newBytesRead,
            ControlRequestUvc.GetCurrent,
            uvcInterface.InterfaceNumber,
            ProcessingUnit,
            ControlSelector,
            timeout: 500
        );
        newReadResult.Should().Be(UsbResult.Success);
        newBytesRead.Should().Be(2);
        BitConverter.ToInt16(newValueBuffer).Should().Be(newValue);
    }

    public void Dispose()
    {
        _usb.Dispose();
    }
}
