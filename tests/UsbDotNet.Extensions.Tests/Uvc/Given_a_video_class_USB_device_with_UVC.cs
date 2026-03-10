using UsbDotNet.Core;
using UsbDotNet.Extensions.Uvc;
using UsbDotNet.Extensions.Uvc.Unix;
using UsbDotNet.Extensions.Uvc.Windows;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Extensions.Tests.Uvc;

[Trait("Category", "UsbVideoControl")]
public sealed class Given_a_video_class_USB_device_with_UVC : IDisposable
{
    private const ushort HuddlyVendorId = 0x2BD9;
    private const byte UvcInterfaceSubClass = UsbDeviceDescriptorExtension.UvcVideoControlSubClass;

    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_video_class_USB_device_with_UVC> _logger;
    private readonly Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_a_video_class_USB_device_with_UVC(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_video_class_USB_device_with_UVC>();
        _usb = new Usb(_libusb, _loggerFactory);
        try
        {
            _usb.Initialize(LogLevel.Information);
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetPreferredVendorId(HuddlyVendorId);
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
    public void GetCameraControlEntityId_returns_valid_entityId()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var uvcInterface = GetFirstUvcInterface(device);
        var entityId = device.GetUvcCameraControlEntityId(uvcInterface.InterfaceNumber);
        entityId.Should().NotBeNull("The device must have a camera terminal to test UVC controls.");
    }

    [SkippableFact]
    public void GetImageSettingEntityId_returns_valid_entityId()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var uvcInterface = GetFirstUvcInterface(device);
        var entityId = device.GetUvcImageSettingEntityId(uvcInterface.InterfaceNumber);
        entityId.Should().NotBeNull("The device must have a processing unit to test UVC controls.");
    }

    [SkippableFact]
    public void SafeVideoDeviceHandle_Open_returns_deviceHandle_on_Windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new SkipException("The SafeVideoDeviceHandle is only available on Windows.");
        }
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var uvcInterface = GetFirstUvcInterface(device);
        using var handle = SafeVideoDeviceHandle.Open(device, uvcInterface.InterfaceNumber);
    }

    [SkippableFact]
    public void ControlReadUvc_Brightness_should_complete_successfully_on_non_Windows_OS()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new SkipException("The UVC control interface is inaccessible on the Windows OS.");
        }
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var serial = device.GetSerialNumber();
        var uvcInterface = GetFirstUvcInterface(device);
        _logger.LogInformation(
            "Video device open: VID=0x{VID:X4}, PID=0x{PID:X4}, "
                + "SerialNumber={SerialNumber}, UVC interface: {Interface}.",
            device.Descriptor.VendorId,
            device.Descriptor.ProductId,
            serial,
            uvcInterface.InterfaceNumber
        );
        var entityId =
            device.GetUvcImageSettingEntityId(uvcInterface.InterfaceNumber)
            ?? throw new InvalidOperationException("Camera control entity ID not found.");
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(
            UvcImageSetting.Brightness
        );
        var readBuffer = new Span<byte>(new byte[bufferSize]);
        var result = device.ControlReadUvc(
            readBuffer,
            out var readLength,
            UvcControlRequest.GetCurrent,
            uvcInterface.InterfaceNumber,
            entityId,
            controlSelector,
            timeout: 500
        );
        var readValue = BitConverter.ToInt16(readBuffer);
        result.Should().Be(UsbResult.Success);
    }

    [SkippableFact]
    public void ControlWriteUvc_Brightness_should_successfully_write_provided_value_to_device_on_non_Windows_OS()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new SkipException("The UVC control interface is inaccessible on the Windows OS.");
        }
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var serial = device.GetSerialNumber();
        var uvcInterface = GetFirstUvcInterface(device);
        _logger.LogInformation(
            "Video device open: VID=0x{VID:X4}, PID=0x{PID:X4}, "
                + "SerialNumber={SerialNumber}, UVC interface: {Interface}.",
            device.Descriptor.VendorId,
            device.Descriptor.ProductId,
            serial,
            uvcInterface.InterfaceNumber
        );
        var entityId =
            device.GetUvcImageSettingEntityId(uvcInterface.InterfaceNumber)
            ?? throw new InvalidOperationException("Camera control entity ID not found.");
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(
            UvcImageSetting.Brightness
        );
        var initialValueBuffer = new Span<byte>(new byte[bufferSize]);
        var initialReadResult = device.ControlReadUvc(
            initialValueBuffer,
            out var initialBytesRead,
            UvcControlRequest.GetCurrent,
            uvcInterface.InterfaceNumber,
            entityId,
            controlSelector,
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
            UvcControlRequest.SetCurrent,
            uvcInterface.InterfaceNumber,
            entityId,
            controlSelector,
            timeout: 500
        );
        writeResult.Should().Be(UsbResult.Success);
        bytesWritten.Should().Be(2);

        var newValueBuffer = new Span<byte>(new byte[2]);
        var newReadResult = device.ControlReadUvc(
            newValueBuffer,
            out var newBytesRead,
            UvcControlRequest.GetCurrent,
            uvcInterface.InterfaceNumber,
            entityId,
            controlSelector,
            timeout: 500
        );
        newReadResult.Should().Be(UsbResult.Success);
        newBytesRead.Should().Be(2);
        BitConverter.ToInt16(newValueBuffer).Should().Be(newValue);
    }

    [SkippableTheory]
    [InlineData(UvcCameraControl.Pan)]
    [InlineData(UvcCameraControl.Tilt)]
    [InlineData(UvcCameraControl.Zoom)]
    public void GetCameraControlRange_returns_valid_range(UvcCameraControl control)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetCameraControlRange(
            control,
            out var min,
            out var max,
            out var step,
            out var defaultValue,
            out var controlFlags
        );
        _logger.LogInformation(
            "{Control} range: min={Min}, max={Max}, step={Step}, default={Default}, flags={ControlFlags}.",
            control,
            min,
            max,
            step,
            defaultValue,
            controlFlags
        );
        min.Should().BeLessOrEqualTo(max);
        // Note: A step of 0 is valid and indicates that the control does not support stepping.
        step.Should().BeGreaterThanOrEqualTo(0);
    }

    [SkippableTheory]
    [InlineData(UvcCameraControl.Pan)]
    [InlineData(UvcCameraControl.Tilt)]
    [InlineData(UvcCameraControl.Zoom)]
    public void GetCameraControl_returns_value_within_reported_range(UvcCameraControl control)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetCameraControlRange(control, out var min, out var max, out _, out _, out _);
        var value = uvc.GetCameraControl(control, out _);
        _logger.LogInformation(
            "{Control}: value={Value}, range=[{Min}, {Max}].",
            control,
            value,
            min,
            max
        );
        value.Should().BeInRange(min, max);
    }

    [SkippableTheory]
    [InlineData(UvcCameraControl.Pan)]
    [InlineData(UvcCameraControl.Tilt)]
    [InlineData(UvcCameraControl.Zoom)]
    public void SetCameraControl_should_roundtrip_value(UvcCameraControl control)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetCameraControlRange(control, out var min, out var max, out var step, out _, out _);
        var originalValue = uvc.GetCameraControl(control, out _);
        var targetValue = originalValue + step <= max ? originalValue + step : originalValue - step;
        try
        {
            uvc.SetCameraControl(control, targetValue);
            var readBack = uvc.GetCameraControl(control, out _);
            _logger.LogInformation(
                "{Control}: original={Original}, target={Target}, readBack={ReadBack}.",
                control,
                originalValue,
                targetValue,
                readBack
            );
            readBack.Should().Be(targetValue);
        }
        finally
        {
            uvc.SetCameraControl(control, originalValue);
        }
    }

    [SkippableTheory]
    [InlineData(UvcImageSetting.Brightness)]
    [InlineData(UvcImageSetting.Saturation)]
    public void GetImageSettingRange_returns_valid_range(UvcImageSetting setting)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetImageSettingRange(
            setting,
            out var min,
            out var max,
            out var step,
            out var defaultValue,
            out _
        );
        _logger.LogInformation(
            "{Setting} range: min={Min}, max={Max}, step={Step}, default={Default}.",
            setting,
            min,
            max,
            step,
            defaultValue
        );
        min.Should().BeLessOrEqualTo(max);
        step.Should().BePositive();
    }

    [SkippableTheory]
    [InlineData(UvcImageSetting.Brightness)]
    [InlineData(UvcImageSetting.Saturation)]
    public void GetImageSetting_returns_value_within_reported_range(UvcImageSetting setting)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetImageSettingRange(setting, out var min, out var max, out _, out _, out _);
        var value = uvc.GetImageSetting(setting, out _);
        _logger.LogInformation(
            "{Setting}: value={Value}, range=[{Min}, {Max}].",
            setting,
            value,
            min,
            max
        );
        value.Should().BeInRange(min, max);
    }

    [SkippableTheory]
    [InlineData(UvcImageSetting.Brightness)]
    [InlineData(UvcImageSetting.Saturation)]
    public void SetImageSetting_should_roundtrip_value(UvcImageSetting setting)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var uvc = OpenFirstUvcControls(device);
        uvc.GetImageSettingRange(setting, out var min, out var max, out var step, out _, out _);
        var originalValue = uvc.GetImageSetting(setting, out _);
        var targetValue = originalValue + step <= max ? originalValue + step : originalValue - step;
        try
        {
            uvc.SetImageSetting(setting, targetValue);
            var readBack = uvc.GetImageSetting(setting, out _);
            _logger.LogInformation(
                "{Setting}: original={Original}, target={Target}, readBack={ReadBack}.",
                setting,
                originalValue,
                targetValue,
                readBack
            );
            readBack.Should().Be(targetValue);
        }
        finally
        {
            uvc.SetImageSetting(setting, originalValue);
        }
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
