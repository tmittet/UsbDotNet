namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDevice;

[Trait("Category", "UsbDevice")]
public class Given_an_accessible_USB_device_Real(ITestOutputHelper output)
    : Given_an_accessible_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_an_accessible_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void TestFailsAfterDispose()
    {
        EnterReadLock(() =>
        {
            var context = GetContext();
            var list = context.GetDeviceList();
            var device = list.GetAccessibleDeviceOrSkipTest();

            list.Dispose();

            Action act = () => device.GetActiveConfigDescriptor();
            act.Should().Throw<ObjectDisposedException>();

            act = () => device.Open();
            act.Should().Throw<ObjectDisposedException>();

            act = () => device.GetBusNumber();
            act.Should().Throw<ObjectDisposedException>();

            act = () => device.GetDeviceAddress();
            act.Should().Throw<ObjectDisposedException>();

            act = () => device.GetPortNumber();
            act.Should().Throw<ObjectDisposedException>();

            act = () => device.GetDeviceDescriptor();
            act.Should().Throw<ObjectDisposedException>();

            // Verify context is closed after dispose
            context.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }
};
