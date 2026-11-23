using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDeviceInterface;

public class Given_a_fake_USB_device
{
    [Fact]
    public void GetInterfaceNumber_returns_the_claimed_interface_number()
    {
        using var context = new LibUsb(new FakeLibusbApi()).CreateContext();
        using var list = context.GetDeviceList();
        using var handle = list[0].Open();
        using var deviceInterface = handle.ClaimInterface(1);

        var interfaceNumber = deviceInterface.GetInterfaceNumber();

        interfaceNumber.Should().Be(1);
    }

    [Fact]
    public void A_claimed_interface_keeps_a_disposed_device_handle_open_until_it_is_released()
    {
        var api = new FakeLibusbApi();
        using var context = new LibUsb(api).CreateContext();
        using var list = context.GetDeviceList();
        var handle = list[0].Open();
        var deviceInterface = handle.ClaimInterface(0);

        // The device handle is disposed before its interface
        handle.Dispose();

        api.CloseCount.Should().Be(0);
        api.ReleaseInterfaceCount.Should().Be(0);

        deviceInterface.Dispose();

        api.ReleaseInterfaceCount.Should().Be(1);
        api.ReleaseInterfaceOnClosedHandleCount.Should().Be(0);
        api.CloseCount.Should().Be(1);
    }

    [Fact]
    public void The_device_handle_is_closed_on_dispose_when_its_interfaces_are_released()
    {
        var api = new FakeLibusbApi();
        using var context = new LibUsb(api).CreateContext();
        using var list = context.GetDeviceList();
        var handle = list[0].Open();
        handle.ClaimInterface(0).Dispose();
        handle.ClaimInterface(1).Dispose();

        handle.Dispose();

        api.ReleaseInterfaceCount.Should().Be(2);
        api.CloseCount.Should().Be(1);
    }

    [Fact]
    public void A_failed_claim_does_not_keep_the_device_handle_open()
    {
        var api = new FakeLibusbApi();
        using var context = new LibUsb(api).CreateContext();
        using var list = context.GetDeviceList();
        var handle = list[0].Open();
        api.InjectError(nameof(api.libusb_claim_interface), libusb_error.LIBUSB_ERROR_BUSY);

        var claim = () => handle.ClaimInterface(0);

        claim.Should().Throw<LibUsbException>();
        handle.Dispose();
        api.CloseCount.Should().Be(1);
    }

    [Fact]
    public void The_device_handle_is_closed_when_the_release_of_its_last_interface_fails()
    {
        var api = new FakeLibusbApi();
        using var context = new LibUsb(api).CreateContext();
        using var list = context.GetDeviceList();
        var handle = list[0].Open();
        var deviceInterface = handle.ClaimInterface(0);
        api.InjectError(nameof(api.libusb_release_interface), libusb_error.LIBUSB_ERROR_OTHER);
        handle.Dispose();

        deviceInterface.Dispose();

        api.ReleaseInterfaceCount.Should().Be(1);
        api.CloseCount.Should().Be(1);
    }
}
