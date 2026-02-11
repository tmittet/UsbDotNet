using UsbDotNet.Core;
using UsbDotNet.Extensions.ControlTransfer;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Transfer;

namespace UsbDotNet.Extensions.Tests.ControlTransfer;

[Trait("Category", "UsbDevice")]
public sealed class Given_any_USB_device : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_any_USB_device> _logger;
    private readonly Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_any_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_any_USB_device>();
        _usb = new Usb(_libusb, _loggerFactory);
        _usb.Initialize(LogLevel.Information);
        _deviceSource = new TestDeviceSource(_logger, _usb);
        _deviceSource.SetPreferredVendorId(0x2BD9);
    }

    [SkippableFact]
    public void ControlRead_returns_expected_descriptor_given_Standard_GetDescriptor_request()
    {
        const byte descriptorTypeDevice = 0x01;
        const ushort descriptorIndex = 0x00;

        using var device = _deviceSource.OpenUsbDeviceOrSkip();

        // Allocate a slightly bigger buffer than the expected 18 bytes,
        // this enables us to test that bytesRead returns expected length.
        var descriptorBuffer = new byte[32];

        var result = device.ControlRead(
            descriptorBuffer,
            out var bytesRead,
            ControlRequestRecipient.Device,
            StandardRequest.GetDescriptor,
            (descriptorTypeDevice << 8) | descriptorIndex
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
    public void ControlWrite_is_successfull_given_Standard_SetConfiguration_request()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();

        // Start by getting current device configuration
        var readBuffer = new byte[1];
        var readResult = device.ControlRead(
            readBuffer,
            out var bytesRead,
            ControlRequestRecipient.Device,
            StandardRequest.GetConfiguration,
            0
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
            StandardRequest.SetConfiguration,
            readBuffer[0]
        );

        using var scope = new AssertionScope();
        writeResult.Should().Be(UsbResult.Success);
        // We did not provide a payload, expect zero bytes written
        bytesWritten.Should().Be(0);
    }

    public void Dispose()
    {
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
