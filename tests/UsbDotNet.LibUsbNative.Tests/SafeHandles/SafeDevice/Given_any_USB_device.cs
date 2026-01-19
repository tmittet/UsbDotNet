using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDevice;

[Trait("Category", "UsbDevice")]
public class Given_any_USB_device_Real(ITestOutputHelper output)
    : Given_any_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_any_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void GetDeviceDescriptor_succeeds_given_active_context()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();
            var descriptor = device.GetDeviceDescriptor();

            // Dispose here to free any native allocations
            list.Dispose();
            context.Dispose();

            descriptor.bDescriptorType.Should().Be(libusb_descriptor_type.LIBUSB_DT_DEVICE);

            // Verify context is closed after dispose
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void GetActiveConfigDescriptor_succeeds_given_active_context()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();
            var descriptor = device.GetActiveConfigDescriptor();

            // Dispose here to free any native allocations
            list.Dispose();
            context.Dispose();

            descriptor.bDescriptorType.Should().Be(libusb_descriptor_type.LIBUSB_DT_CONFIG);

            // Verify context is closed after dispose
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void GetDeviceDescriptor_throws_given_disposed_context()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            list.Dispose();
            context.Dispose();

            var act = () => device.GetDeviceDescriptor();
            act.Should().Throw<InvalidOperationException>();
        });
    }

    [SkippableFact]
    public void GetActiveConfigDescriptor_throws_given_disposed_context()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            list.Dispose();
            context.Dispose();

            var act = () => device.GetActiveConfigDescriptor();
            act.Should().Throw<InvalidOperationException>();
        });
    }
};
