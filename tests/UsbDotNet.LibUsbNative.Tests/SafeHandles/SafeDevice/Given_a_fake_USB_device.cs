using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDevice;

public class Given_a_fake_USB_device
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task GetSpeed_keeps_the_device_alive_when_it_is_disposed_during_the_native_call()
    {
        var api = new FakeLibusbApi { Speed = libusb_speed.LIBUSB_SPEED_SUPER };
        var speedRequests = new List<IntPtr>();
        using var enteredNativeCall = new ManualResetEventSlim();
        using var releaseNativeCall = new ManualResetEventSlim();
        api.OnGetDeviceSpeed = devicePtr =>
        {
            speedRequests.Add(devicePtr);
            enteredNativeCall.Set();
            releaseNativeCall.Wait();
        };
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();
        var device = list[0];

        var getSpeed = Task.Run(device.GetSpeed);
        try
        {
            enteredNativeCall.Wait(Timeout).Should().BeTrue();

            // Dispose everything that owns the native device while the native call is in progress
            list.Dispose();
            context.Dispose();

            api.UnrefDevices.Should().BeEmpty();
            api.ExitCount.Should().Be(0);
        }
        finally
        {
            releaseNativeCall.Set();
        }

        (await getSpeed).Should().Be(libusb_speed.LIBUSB_SPEED_SUPER);
        speedRequests.Should().Equal(api.DevicePtr);
        // The device, and the context that it keeps alive, are released when the call completes
        api.UnrefDevices.Should().Equal(api.DevicePtr);
        api.ExitCount.Should().Be(1);
    }
}
