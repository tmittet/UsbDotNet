using UsbDotNet.Core;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Transfer;

namespace UsbDotNet.Tests.UsbDevice;

[Trait("Category", "UsbDevice")]
public sealed class Given_any_USB_device : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_any_USB_device> _logger;
    private readonly UsbDotNet.Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_any_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_any_USB_device>();
        _usb = new UsbDotNet.Usb(_libusb, _loggerFactory);
        try
        {
            _usb.Initialize(LogLevel.Information);
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetPreferredVendorId(0x2BD9);
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [SkippableFact]
    public void GetSerialNumber_returns_serial_given_an_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var serial = device.GetSerialNumber();
        _logger.LogInformation(
            "Device open: VID=0x{VID:X4}, PID=0x{PID:X4}, SerialNumber={SerialNumber}.",
            device.Descriptor.VendorId,
            device.Descriptor.ProductId,
            serial
        );
        serial.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public void GetManufacturer_returns_manufacturer_given_an_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var manufacturer = device.GetManufacturer();
        manufacturer.Should().NotBeNullOrEmpty();
        _ = device.GetManufacturer();
    }

    [SkippableFact]
    public void GetProductName_returns_product_name_given_an_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var productName = device.GetProduct();
        productName.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public void ControlRead_returns_expected_descriptor_given_correct_Device_GetDescriptor_params()
    {
        const byte GetDescriptorRequest = 0x06;
        const byte DeviceDescriptorType = 0x01;
        const ushort DescriptorIndex = 0x00;

        using var device = _deviceSource.OpenUsbDeviceOrSkip();

        // Allocate a slightly bigger buffer than the expected 18 bytes,
        // this enables us to test that bytesRead returns expected length.
        var descriptorBuffer = new byte[32];

        var result = device.ControlRead(
            descriptorBuffer,
            out var bytesRead,
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            GetDescriptorRequest,
            (DeviceDescriptorType << 8) | DescriptorIndex,
            0 // Always zero for Device, GetDescriptor
        );

        using var scope = new AssertionScope();
        result.Should().Be(UsbResult.Success);

        // USB Descriptor is always 18 bytes
        bytesRead.Should().Be(18);
        // Byte 4 is device class
        ((UsbClass)descriptorBuffer[4])
            .Should()
            .Be(device.Descriptor.DeviceClass);
        // Byte 8-9 is vendor ID
        BitConverter.ToUInt16(descriptorBuffer[8..10], 0).Should().Be(device.Descriptor.VendorId);
        // Byte 10-11 is product ID
        BitConverter
            .ToUInt16(descriptorBuffer[10..12], 0)
            .Should()
            .Be(device.Descriptor.ProductId);
    }

    [SkippableFact]
    public void ControlWrite_is_successful_given_params_to_set_current_Configuration()
    {
        const byte GetConfigurationRequest = 0x08;
        const byte SetConfigurationRequest = 0x09;

        using var device = _deviceSource.OpenUsbDeviceOrSkip();

        // Start by getting current device configuration
        var readBuffer = new byte[1];
        var readResult = device.ControlRead(
            readBuffer,
            out var bytesRead,
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            GetConfigurationRequest,
            0, // Always zero for Device, GetConfigurationRequest
            0 // Always zero for Device, GetConfigurationRequest
        );
        readResult
            .Should()
            .Be(UsbResult.Success, "The write test can't continue when read is unsuccessful.");
        bytesRead
            .Should()
            .Be(1, "The write test can't continue when an invalid number of bytes are read.");

        // When configuration read is successful, write the same config value back to the device
        var writeResult = device.ControlWrite(
            [],
            out var bytesWritten,
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            SetConfigurationRequest,
            readBuffer[0],
            0 // Always zero for Device, SetConfigurationRequest
        );

        using var scope = new AssertionScope();
        writeResult.Should().Be(UsbResult.Success);
        // We did not provide a payload, expect zero bytes written
        bytesWritten.Should().Be(0);
    }

    public void Dispose()
    {
        _usb.Dispose();
    }
}
