using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Transfer;

namespace UsbDotNet.Tests.UsbDevice;

[Trait("Category", "UsbVendorClassDevice")]
public sealed class Given_a_vendor_class_USB_device : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_vendor_class_USB_device> _logger;
    private readonly UsbDotNet.Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_a_vendor_class_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_vendor_class_USB_device>();
        _usb = new UsbDotNet.Usb(
            _libusb,
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.Information }
        );
        try
        {
            _usb.Initialize();
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetPreferredVendorId(0x2BD9);
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
    public void Device_has_vendor_interface_with_input_and_output_endpoints()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var vendorInterfaces = device.GetInterfaceDescriptorList(UsbClass.VendorSpecific);
        vendorInterfaces.Should().ContainSingle("Test device must have one vendor interface");
        vendorInterfaces.Single().GetEndpoint(UsbEndpointDirection.Input, out var inputCount);
        inputCount
            .Should()
            .BeGreaterThanOrEqualTo(
                1,
                "Test device vendor interface must have one or more read (host input) endpoints"
            );
        vendorInterfaces.Single().GetEndpoint(UsbEndpointDirection.Output, out var outputCount);
        outputCount
            .Should()
            .BeGreaterThanOrEqualTo(
                1,
                "Test device vendor interface must have one or more write (host output) endpoints"
            );
    }

    [SkippableFact]
    public void Device_is_able_to_claim_interface_and_get_an_input_endpoint()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var endpointFound = usbInterface.TryGetInputEndpoint(out var endpoint);
        endpointFound.Should().BeTrue();
        endpoint!.MaxPacketSize.Should().BePositive();
    }

    [SkippableFact]
    public void Device_throws_InvalidOperationException_when_trying_to_claim_interface_twice()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var act = () => device.ClaimInterface(UsbClass.VendorSpecific);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already claimed*");
    }

    [SkippableFact]
    public void Device_is_able_to_claim_interface_and_get_an_output_endpoint()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        using var usbInterface = device.ClaimInterface(UsbClass.VendorSpecific);
        var endpointFound = usbInterface.TryGetOutputEndpoint(out var endpoint);
        endpointFound.Should().BeTrue();
        endpoint!.MaxPacketSize.Should().BePositive();
    }

    [SkippableFact]
    public void Open_interfaces_are_auto_disposed_when_UsbDevice_is_disposed()
    {
        // Open device and leave it open
        var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Claim interface without disposing it
        _ = device.ClaimInterface(UsbClass.VendorSpecific);
        // Dispose device
        device.Dispose();
    }

    [SkippableFact]
    public void GetInterfaceDescriptors_returns_at_least_one_vendor_interface()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var vendorInterfaces = device.GetInterfaceDescriptors(UsbClass.VendorSpecific);
        vendorInterfaces.Should().NotBeEmpty();
    }

    [SkippableFact]
    public void GetInterfaceDescriptors_returns_interface_of_specified_sub_class()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var interfaceSubClass = device
            .GetInterfaceDescriptorList(UsbClass.VendorSpecific)
            .Select(i => i.InterfaceSubClass)
            .FirstOrDefault();
        // Get interfaces with specific class and sub-class
        var vendorInterfaces = device.GetInterfaceDescriptors(
            UsbClass.VendorSpecific,
            subClass: interfaceSubClass
        );
        vendorInterfaces.Should().NotBeEmpty();
    }

    [SkippableFact]
    public void ControlWrite_is_successful_given_params_to_clear_endpoint_zero_halt()
    {
        const byte clearFeatureRequest = 0x01;
        const ushort endpointHaltFeature = 0x00;

        using var device = _deviceSource.OpenUsbDeviceOrSkip();

        // CLEAR_FEATURE(ENDPOINT_HALT) on endpoint 0 is a harmless no-op:
        // EP0 is never halted, there is no data stage, and no kernel-cached
        // device state (configuration, altsetting, data toggles) is affected.
        // Note: SET_CONFIGURATION is unsuitable here; libusb fakes its success
        // on Windows without ever reaching the device, and on Linux the raw
        // request bypasses and corrupts the kernel's cached device state.
        var writeResult = device.ControlWrite(
            [],
            out var bytesWritten,
            ControlRequestRecipient.Endpoint,
            ControlRequestType.Standard,
            clearFeatureRequest,
            endpointHaltFeature,
            0 // wIndex = endpoint address 0
        );

        // Halt support on EP0 is optional in the USB spec, and some driver
        // stacks (e.g. WinUSB) reserve standard CLEAR_FEATURE requests, so a
        // stall is a valid response; it only means this particular device
        // can't be used to verify a successful control write.
        Skip.If(
            writeResult == UsbResult.PipeError,
            $"Device '{device.Descriptor.DeviceKey}' or its driver stack "
                + "rejected CLEAR_FEATURE(ENDPOINT_HALT) on endpoint 0."
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
