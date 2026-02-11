using System.Text;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Tests.UsbDevice;

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
    public void GetSerialNumber_returns_serial_given_an_open_Huddly_device()
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
    public void GetManufacturer_returns_manufacturer_given_an_open_Huddly_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var manufacturer = device.GetManufacturer();
        manufacturer.Should().Be("Huddly");
    }

    [SkippableFact]
    public void GetProductName_returns_product_name_given_an_open_Huddly_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var productName = device.GetProduct();
        productName.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public void Huddly_device_has_vendor_interface_with_exactly_one_input_and_one_output_endpoint()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var vendorInterfaces = device.GetInterfaceDescriptorList(UsbClass.VendorSpecific);
        vendorInterfaces.Should().ContainSingle("Huddly device has one vendor interface");
        vendorInterfaces.Single().GetEndpoint(UsbEndpointDirection.Input, out var inputCount);
        inputCount.Should().Be(1, "Huddly vendor interface has one read (host input) endpoint");
        vendorInterfaces.Single().GetEndpoint(UsbEndpointDirection.Output, out var outputCount);
        outputCount.Should().Be(1, "Huddly vendor interface has one write (host output) endpoint");
    }

    [SkippableFact]
    public void Huddly_device_is_able_to_claim_interface_and_get_an_input_endpoint()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var endpointFound = usbInterface.TryGetInputEndpoint(out var endpoint);
        endpointFound.Should().BeTrue();
        endpoint!.MaxPacketSize.Should().BePositive();
    }

    [SkippableFact]
    public void Huddly_device_is_able_to_claim_interface_and_get_an_output_endpoint()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var endpointFound = usbInterface.TryGetOutputEndpoint(out var endpoint);
        endpointFound.Should().BeTrue();
        endpoint!.MaxPacketSize.Should().BePositive();
    }

    [SkippableTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void It_is_able_to_send_salute_to_Huddly_device(int _)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        // Send Huddly "reset"
        usbInterface.BulkWrite([], 0, out _, 200).Should().Be(UsbResult.Success);
        usbInterface.BulkWrite([], 0, out _, 200).Should().Be(UsbResult.Success);
        // Send Huddly "salute"
        var writeError = usbInterface.BulkWrite([0x00], 1, out var writeLength, 200);
        writeError.Should().Be(UsbResult.Success);
        writeLength.Should().Be(1);
    }

    [SkippableTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void It_is_able_to_send_salute_and_receive_response_from_Huddly_device(int _)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        // Send Huddly "reset"
        usbInterface.BulkWrite([], 0, out _, 200).Should().Be(UsbResult.Success);
        usbInterface.BulkWrite([], 0, out _, 200).Should().Be(UsbResult.Success);
        // Send Huddly "salute"
        var writeError = usbInterface.BulkWrite([0x00], 1, out var writeLength, 200);
        writeError.Should().Be(UsbResult.Success);
        writeLength.Should().Be(1);
        // Wait for salute response
        const string expectedSaluteResponse = "HLink v0";
        var expectedSaluteBytes = Encoding.UTF8.GetBytes(expectedSaluteResponse);
        var buffer = new byte[8];
        var readError = usbInterface.BulkRead(buffer, out var readLength, 1000);
        readError.Should().Be(UsbResult.Success);
        readLength.Should().Be(expectedSaluteBytes.Length);
        buffer.Should().BeEquivalentTo(expectedSaluteBytes);
    }

    [SkippableTheory(Timeout = 10000)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Disposing_the_USB_interface_cancels_an_ongoing_Huddly_device_transfer(int _)
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var readTask = Task.Run(() =>
        {
            var buffer = new byte[32 * 1024];
            // Wait forever for data
            var error = usbInterface.BulkRead(buffer, out var transferLength, Timeout.Infinite);
        });
        // Dispose USB interface and cancel task before the 30 second timeout
        usbInterface.Dispose();
        // Await read task until completion
        await readTask;
    }

    public void Dispose()
    {
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
