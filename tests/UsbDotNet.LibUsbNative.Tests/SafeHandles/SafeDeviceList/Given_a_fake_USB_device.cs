namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDeviceList;

public class Given_a_fake_USB_device
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Indexer_keeps_the_list_alive_when_it_is_disposed_while_the_devices_are_created()
    {
        var api = new FakeLibusbApi();
        using var enteredNativeCall = new ManualResetEventSlim();
        using var releaseNativeCall = new ManualResetEventSlim();
        // The devices are created from the native list on first access, which takes a reference
        // on each native device
        api.CallHooks[nameof(api.libusb_ref_device)] = () =>
        {
            enteredNativeCall.Set();
            releaseNativeCall.Wait();
        };
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();

        var getDevice = Task.Run(() => list[0]);
        try
        {
            enteredNativeCall.Wait(Timeout).Should().BeTrue();

            // Dispose the list, and the context, on another thread while the devices are created.
            // A disposal that waits for the access to complete fails the test.
            await Task.Run(() =>
                {
                    list.Dispose();
                    context.Dispose();
                })
                .WaitAsync(Timeout);

            api.FreeDeviceListCount.Should().Be(0);
            api.UnrefDevices.Should().BeEmpty();
            api.ExitCount.Should().Be(0);
        }
        finally
        {
            releaseNativeCall.Set();
        }

        // The access completes, and then the list is freed along with the device it created
        var device = await getDevice;
        device.IsClosed.Should().BeTrue();
        api.FreeDeviceListCount.Should().Be(1);
        api.UnrefDevices.Should().Equal(api.DevicePtr);
        api.ExitCount.Should().Be(1);
    }

    [Fact]
    public void Indexer_returns_the_device_of_an_open_list()
    {
        using var context = new LibUsb(new FakeLibusbApi()).CreateContext();
        using var list = context.GetDeviceList();

        var device = list[0];

        device.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Indexer_throws_when_the_list_is_disposed()
    {
        using var context = new LibUsb(new FakeLibusbApi()).CreateContext();
        var list = context.GetDeviceList();
        list.Dispose();

        var act = () => list[0];

        act.Should().Throw<ObjectDisposedException>();
    }
}
