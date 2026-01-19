using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative.Tests.Extensions.DescriptorToStringExtension;

public class Given_any_USB_device_Fake(ITestOutputHelper output)
    : Given_any_USB_device(output, new FakeLibusbApi());

[Trait("Category", "UsbDevice")]
public class Given_any_USB_device_Real(ITestOutputHelper output)
    : Given_any_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_any_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void TestDeviceDescriptorTreeString()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            var descriptor = device.GetDeviceDescriptor();
            var treeString = descriptor.ToTreeString();
            Output.WriteLine(treeString);
            treeString.Should().NotBeNullOrWhiteSpace();
        });
    }

    [SkippableFact]
    public void TestActiveConfigTreeString()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            var descriptor = device.GetActiveConfigDescriptor();
            var treeString = descriptor.ToTreeString();
            Output.WriteLine(treeString);
            treeString.Should().NotBeNullOrWhiteSpace();
        });
    }
};
