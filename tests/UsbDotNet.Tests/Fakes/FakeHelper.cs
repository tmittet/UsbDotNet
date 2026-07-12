using FakeItEasy;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Tests.Fakes;

/// <summary>
/// Factory methods for the FakeItEasy fakes shared across UsbDotNet.Tests, covering the libusb
/// abstraction layer: <see cref="ISafeDevice"/>, <see cref="ISafeDeviceList"/>,
/// <see cref="ISafeContext"/> and <see cref="ILibUsb"/>.
/// </summary>
internal static class FakeHelper
{
    /// <summary>Creates a fake device with the given descriptor and topology.</summary>
    public static ISafeDevice CreateFakeDevice(
        ushort vendorId,
        ushort productId,
        byte busNumber,
        byte busAddress,
        byte portNumber = 1,
        ushort bcdUsb = 0x0200
    )
    {
        var device = A.Fake<ISafeDevice>();
        A.CallTo(() => device.GetDeviceDescriptor())
            .Returns(
                new libusb_device_descriptor(
                    bLength: 18,
                    libusb_descriptor_type.LIBUSB_DT_DEVICE,
                    bcdUsb,
                    libusb_class_code.LIBUSB_CLASS_PER_INTERFACE,
                    bDeviceSubClass: 0,
                    bDeviceProtocol: 0,
                    bMaxPacketSize0: 64,
                    vendorId,
                    productId,
                    bcdDevice: 0x0100,
                    iManufacturer: 1,
                    iProduct: 2,
                    iSerialNumber: 3,
                    bNumConfigurations: 1
                )
            );
        A.CallTo(() => device.GetBusNumber()).Returns(busNumber);
        A.CallTo(() => device.GetDeviceAddress()).Returns(busAddress);
        A.CallTo(() => device.GetPortNumber()).Returns(portNumber);
        return device;
    }

    /// <summary>Creates a fake device list backed by the given devices (empty when omitted).</summary>
    public static ISafeDeviceList CreateFakeDeviceList(IReadOnlyList<ISafeDevice>? devices = null)
    {
        devices ??= [];
        var deviceList = A.Fake<ISafeDeviceList>();
        A.CallTo(() => deviceList.Count).Returns(devices.Count);
        A.CallTo(() => deviceList[A<int>._]).ReturnsLazily((int index) => devices[index]);
        A.CallTo(() => deviceList.GetEnumerator()).ReturnsLazily(() => devices.GetEnumerator());
        return deviceList;
    }

    /// <summary>
    /// Creates a fake context whose event loop unblocks the normal way: HandleEventsCompleted
    /// blocks until InterruptEventHandler is called, exactly as Usb.Dispose drives it.
    /// GetDeviceList returns the given devices.
    /// </summary>
    public static ISafeContext CreateFakeContext(IReadOnlyList<ISafeDevice> devices)
    {
        var context = A.Fake<ISafeContext>();
#pragma warning disable CA2000 // Disposed by the context Dispose callback configured below
        var interrupted = new SemaphoreSlim(0);
#pragma warning restore CA2000
        var closed = false;
        A.CallTo(() => context.GetDeviceList()).ReturnsLazily(() => CreateFakeDeviceList(devices));
        // Block the event loop thread until interrupted, like the real blocking call
        A.CallTo(() => context.HandleEventsCompleted(A<nint>._))
            .ReturnsLazily(() =>
            {
                interrupted.Wait();
                return libusb_error.LIBUSB_SUCCESS;
            });
        A.CallTo(() => context.InterruptEventHandler()).Invokes(() => interrupted.Release());
        // The event loop thread is joined before the context is disposed,
        // making it safe to dispose the semaphore here
        A.CallTo(() => context.Dispose())
            .Invokes(() =>
            {
                closed = true;
                interrupted.Dispose();
            });
        A.CallTo(() => context.IsClosed).ReturnsLazily(() => closed);
        return context;
    }

    /// <summary>
    /// Creates a fake context with a gated event loop: HandleEventsCompleted signals
    /// <paramref name="entered"/> and then parks the event-loop thread until
    /// <paramref name="release"/> is set, ignoring InterruptEventHandler. This lets a test
    /// deterministically hold Usb.Dispose in the window where it joins the event-loop thread.
    /// Device enumeration is empty.
    /// </summary>
    public static ISafeContext CreateBlockingFakeContext(
        ManualResetEventSlim entered,
        ManualResetEventSlim release
    )
    {
        var context = A.Fake<ISafeContext>();
        var closed = false;
        A.CallTo(() => context.HandleEventsCompleted(A<nint>._))
            .ReturnsLazily(() =>
            {
                entered.Set();
                release.Wait();
                return libusb_error.LIBUSB_SUCCESS;
            });
        A.CallTo(() => context.GetDeviceList()).ReturnsLazily(() => CreateFakeDeviceList());
        // Simulates libusb_exit; only flips IsClosed
        A.CallTo(() => context.Dispose()).Invokes(() => closed = true);
        A.CallTo(() => context.IsClosed).ReturnsLazily(() => closed);
        return context;
    }

    /// <summary>Creates a fake ILibUsb whose CreateContext returns the given context.</summary>
    public static ILibUsb CreateFakeLibUsb(ISafeContext context, bool hasCapability = false)
    {
        var libUsb = A.Fake<ILibUsb>();
        A.CallTo(() => libUsb.CreateContext()).Returns(context);
        A.CallTo(() => libUsb.HasCapability(A<libusb_capability>._)).Returns(hasCapability);
        return libUsb;
    }

    /// <summary>Creates a fake ILibUsb serving the given devices from a fresh context.</summary>
    public static ILibUsb CreateFakeLibUsb(params ISafeDevice[] devices)
    {
        var libUsb = A.Fake<ILibUsb>();
        // Deferred: CreateFakeContext is only ever invoked (once) via this callback, so the
        // context it allocates is owned and disposed by whoever disposes the resulting Usb.
        A.CallTo(() => libUsb.CreateContext()).ReturnsLazily(() => CreateFakeContext(devices));
        return libUsb;
    }

    /// <summary>Creates a fake ILibUsbApi driving the hotplug DEVICE_ARRIVED / DEVICE_LEFT path.</summary>
    public static FakeHotplugLibUsbApi CreateHotplugLibUsbApi(ushort vendorId, ushort productId) =>
        new(vendorId, productId);
}
