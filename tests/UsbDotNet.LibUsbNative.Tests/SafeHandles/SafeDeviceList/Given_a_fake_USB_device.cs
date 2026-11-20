namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDeviceList;

public class Given_a_fake_USB_device
{
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
