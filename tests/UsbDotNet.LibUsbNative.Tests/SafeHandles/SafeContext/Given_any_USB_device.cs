namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeContext;

public class Given_any_USB_device_Fake(ITestOutputHelper output)
    : Given_any_USB_device(output, new FakeLibusbApi());

[Trait("Category", "UsbDevice")]
public class Given_any_USB_device_Real(ITestOutputHelper output)
    : Given_any_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_any_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void Opening_two_SafeContexts_is_successful_and_closes_properly_on_dispose()
    {
        EnterReadLock(() =>
        {
            var context1 = GetContext();
            var context2 = GetContext();

            var list1 = context1.GetDeviceList();
            _ = list1.GetAnyDeviceOrSkipTest();

            var list2 = context2.GetDeviceList();
            list1.Count.Should().BePositive();
            list2.Count.Equals(list1.Count);

            context1.Dispose();
            context2.Dispose();

            _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));

            context1.IsClosed.Should().BeFalse();
            context2.IsClosed.Should().BeFalse();

            // Verify context is closed after dispose
            list1.Dispose();
            list2.Dispose();
            context1.IsClosed.Should().BeTrue();
            context2.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void GetDeviceList_returns_one_or_more_devices_and_closes_properly_on_dispose()
    {
        EnterReadLock(() =>
        {
            var context = GetContext();
            var list = context.GetDeviceList();
            _ = list.GetAnyDeviceOrSkipTest();

            list.Count.Should().BePositive();
            list.Should().HaveCount(list.Count);

            list.Dispose();
            context.Dispose();

            _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));

            // Verify context is closed after dispose
            context.IsClosed.Should().BeTrue();
        });
    }
};
