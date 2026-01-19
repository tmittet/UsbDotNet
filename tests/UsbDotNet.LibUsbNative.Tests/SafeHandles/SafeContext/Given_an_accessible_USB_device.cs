namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeContext;

public class Given_an_accessible_USB_device_Fake(ITestOutputHelper output)
    : Given_an_accessible_USB_device(output, new FakeLibusbApi());

[Trait("Category", "UsbDevice")]
public class Given_an_accessible_USB_device_Real(ITestOutputHelper output)
    : Given_an_accessible_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_an_accessible_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void Context_dispose_is_blocked_until_SafeDeviceList_and_SafeDeviceHandle_are_disposed()
    {
        EnterWriteLock(() =>
        {
            var context = GetContext();
            var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            var deviceHandle = device.Open();
            context.Dispose();
            _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));
            context.IsClosed.Should().BeFalse();

            // Verify context is closed after dispose
            deviceHandle.Dispose();
            deviceList.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void The_order_of_SafeDeviceList_and_SafeDeviceHandle_disposal_does_not_matter()
    {
        EnterWriteLock(() =>
        {
            var context = GetContext();
            var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            var deviceHandle = device.Open();

            context.Dispose();
            deviceList.Dispose();

            deviceHandle.IsClosed.Should().BeFalse();
            deviceHandle.Dispose();
            _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));

            // Verify context is closed after dispose
            context.IsClosed.Should().BeTrue();
        });
    }
};
