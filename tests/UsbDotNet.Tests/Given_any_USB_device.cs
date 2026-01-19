using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Transfer;

namespace UsbDotNet.Tests;

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
    public void GetDeviceList_returns_at_least_one_USB_device()
    {
        var descriptors = _usb.GetDeviceList();
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
    public void OpenDevice_throws_UsbException_given_invalid_device_key()
    {
        var invalidDeviceKey = UsbDeviceDescriptor.GetKey(0xFFFF, 0xFFFF, 255, 255);
        var act = () => _usb.OpenDevice(invalidDeviceKey);
        act.Should()
            .Throw<UsbException>()
            .WithMessage("Failed to get device from list; the device could not be found.");
    }

    [SkippableFact]
    public void OpenDevice_throws_InvalidOperationException_when_device_is_already_open()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;
        var act = () => _usb.OpenDevice(deviceDescriptor);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{deviceDescriptor.DeviceKey}' already open.");
    }

    [SkippableFact]
    public void OpenDevice_is_able_to_find_device_based_on_device_key()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;

        // This is expected to throw InvalidOperationException; since the device is already open.
        // The test proves OpenDevice finds the device; another exception type with a different
        // error message would be thrown if the device key was invalid or device was not found.
        var act = () => _usb.OpenDevice(deviceDescriptor.DeviceKey);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{deviceDescriptor.DeviceKey}' already open.");
    }

    [SkippableFact]
    public void OpenDevice_is_able_to_find_device_based_on_VID_PID_bus_number_and_address()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;
        var validDeviceKey = UsbDeviceDescriptor.GetKey(
            deviceDescriptor.VendorId,
            deviceDescriptor.ProductId,
            deviceDescriptor.BusNumber,
            deviceDescriptor.BusAddress
        );

        // This is expected to throw InvalidOperationException; since the device is already open.
        // The test proves OpenDevice finds the device; another exception type with a different
        // error message would be thrown if the device key was invalid or device was not found.
        var act = () => _usb.OpenDevice(validDeviceKey);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{validDeviceKey}' already open.");
    }

    [SkippableFact]
    public void GetDeviceSerial_returns_serial_given_a_device_descriptor_when_device_is_not_open()
    {
        IUsbDeviceDescriptor deviceDescriptor;
        using (var device = _deviceSource.OpenUsbDeviceOrSkip())
        {
            deviceDescriptor = device.Descriptor;
        }
        var serial = _usb.GetDeviceSerial(deviceDescriptor);
        serial.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public void GetDeviceSerial_succeeds_given_a_device_descriptor_when_device_is_already_open()
    {
        using var openDevice = _deviceSource.OpenUsbDeviceOrSkip();
        _logger.LogInformation(
            "Device open: VID=0x{VID:X4}, PID=0x{PID:X4}, SerialNumber={SerialNumber}.",
            openDevice.Descriptor.VendorId,
            openDevice.Descriptor.ProductId,
            openDevice.GetSerialNumber()
        );
        // Get serial using the descriptor (not the open device)
        var serial = _usb.GetDeviceSerial(openDevice.Descriptor);
        serial.Should().NotBeNullOrWhiteSpace();
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
    public void Open_devices_are_auto_disposed_when_the_Usb_type_is_disposed()
    {
        // Open device and leave it open
        var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Dispose Usb to trigger auto disposal of devices
        _usb.Dispose();
        // Attempt to get serial, the device should be auto disposed at this point
        var getSerialAct = () => device.GetSerialNumber();
        getSerialAct.Should().Throw<ObjectDisposedException>();
        var disposeAct = () => device.Dispose();
#if DEBUG
        // Calling dispose in debug throws exception
        disposeAct.Should().Throw<ObjectDisposedException>();
#else
        // Calling dispose again in release only logs warning
        disposeAct.Should().NotThrow();
#endif
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
    public void ControlWrite_is_successfull_given_params_to_set_current_Configuration()
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
