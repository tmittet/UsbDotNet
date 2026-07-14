using FakeItEasy;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Tests.Fakes;

/// <summary>
/// Fake of the <see cref="ILibUsbApi"/> members exercised by the hotplug DEVICE_ARRIVED /
/// DEVICE_LEFT path; everything else keeps FakeItEasy's do-nothing defaults. Bus number, device
/// address and port number are mutable so a test can simulate them becoming unreadable after a
/// device has left. Create via <see cref="FakeHelper.CreateHotplugLibUsbApi"/>.
/// </summary>
internal sealed class FakeHotplugLibUsbApi : IDisposable
{
    private Action? _onNextHandleEventsCompleted;

    public ILibUsbApi Api { get; }

    // Mutable topology: a test can change these between arrival and removal to prove the value
    // reported on DEVICE_LEFT comes from the cache captured on arrival, not a fresh (stale) read.
    public byte BusNumber { get; set; } = 3;
    public byte DeviceAddress { get; set; } = 17;
    public byte PortNumber { get; set; } = 1;

    /// <summary>
    /// When set, the event-loop thread signals <see cref="EventLoopEntered"/> and blocks inside
    /// libusb_handle_events_completed until <see cref="EventLoopRelease"/> is set, simulating an
    /// event-loop thread that cannot exit (e.g. stuck dispatching a hotplug event).
    /// </summary>
    public bool BlockEventLoop { get; set; }

    public ManualResetEventSlim EventLoopEntered { get; } = new(false);
    public ManualResetEventSlim EventLoopRelease { get; } = new(false);

    /// <summary>The last hotplug callback registered; null once deregistered.</summary>
    public libusb_hotplug_callback_fn? LastCallback { get; private set; }

    /// <summary>
    /// Runs <paramref name="action"/> once, on the real event-loop thread, from inside the next
    /// call to libusb_handle_events_completed, exactly as real libusb dispatches a pending
    /// hotplug event. Lets a test simulate <see cref="LastCallback"/> being invoked from the
    /// event-loop thread itself instead of from the calling (test) thread.
    /// </summary>
    public void RunOnNextHandleEventsCompleted(Action action) =>
        _onNextHandleEventsCompleted = action;

    public FakeHotplugLibUsbApi(ushort vendorId, ushort productId)
    {
        var descriptor = new libusb_device_descriptor(
            bLength: 18,
            bDescriptorType: libusb_descriptor_type.LIBUSB_DT_DEVICE,
            bcdUSB: 0x0200,
            bDeviceClass: libusb_class_code.LIBUSB_CLASS_MISCELLANEOUS,
            bDeviceSubClass: 0x02,
            bDeviceProtocol: 0x01,
            bMaxPacketSize0: 64,
            idVendor: vendorId,
            idProduct: productId,
            bcdDevice: 0x0100,
            iManufacturer: 1,
            iProduct: 2,
            iSerialNumber: 3,
            bNumConfigurations: 1
        );

        var api = A.Fake<ILibUsbApi>();
        var ctx = IntPtr.Zero;
        A.CallTo(() => api.libusb_init(out ctx))
            .Returns(libusb_error.LIBUSB_SUCCESS)
            .AssignsOutAndRefParameters(new IntPtr(0xC0DE));
        A.CallTo(() => api.libusb_has_capability(A<libusb_capability>._)).Returns(1);
        A.CallTo(() => api.libusb_handle_events_completed(A<IntPtr>._, A<IntPtr>._))
            .ReturnsLazily(() =>
            {
                Interlocked.Exchange(ref _onNextHandleEventsCompleted, null)?.Invoke();
                if (BlockEventLoop)
                {
                    EventLoopEntered.Set();
                    EventLoopRelease.Wait();
                }
                // Real libusb blocks here; the fake would otherwise busy-spin the event-loop thread.
                Thread.Sleep(5);
                return libusb_error.LIBUSB_SUCCESS;
            });
        var outDescriptor = default(libusb_device_descriptor);
        A.CallTo(() => api.libusb_get_device_descriptor(A<IntPtr>._, out outDescriptor))
            .Returns(libusb_error.LIBUSB_SUCCESS)
            .AssignsOutAndRefParameters(descriptor);
        A.CallTo(() => api.libusb_get_bus_number(A<IntPtr>._)).ReturnsLazily(() => BusNumber);
        A.CallTo(() => api.libusb_get_device_address(A<IntPtr>._))
            .ReturnsLazily(() => DeviceAddress);
        A.CallTo(() => api.libusb_get_port_number(A<IntPtr>._)).ReturnsLazily(() => PortNumber);
        var callbackHandle = IntPtr.Zero;
        A.CallTo(() =>
                api.libusb_hotplug_register_callback(
                    A<IntPtr>._,
                    A<libusb_hotplug_event>._,
                    A<libusb_hotplug_flag>._,
                    A<int>._,
                    A<int>._,
                    A<int>._,
                    A<libusb_hotplug_callback_fn>._,
                    A<IntPtr>._,
                    out callbackHandle
                )
            )
            .ReturnsLazily(call =>
            {
                LastCallback = call.GetArgument<libusb_hotplug_callback_fn>(6);
                return libusb_error.LIBUSB_SUCCESS;
            })
            .AssignsOutAndRefParameters(new IntPtr(42));
        A.CallTo(() => api.libusb_hotplug_deregister_callback(A<IntPtr>._, A<IntPtr>._))
            .Invokes(() => LastCallback = null);
        Api = api;
    }

    public void Dispose()
    {
        EventLoopEntered.Dispose();
        EventLoopRelease.Dispose();
    }
}
