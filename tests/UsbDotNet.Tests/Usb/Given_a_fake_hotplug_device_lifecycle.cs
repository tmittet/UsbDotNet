using UsbDotNet.Descriptor;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.Tests.Fakes;

namespace UsbDotNet.Tests.Usb;

/// <summary>
/// Drives the hotplug callback over a fake libusb API (no hardware) to exercise the
/// DEVICE_ARRIVED / DEVICE_LEFT descriptor handling directly.
/// </summary>
public sealed class Given_a_fake_hotplug_device_lifecycle : IDisposable
{
    private const ushort VendorId = 0x1234;
    private const ushort ProductId = 0x5678;

    private static readonly IntPtr DevicePtr = new(0x1000);

    private readonly ILoggerFactory _loggerFactory;
    private readonly FakeHotplugLibUsbApi _api = FakeHelper.CreateHotplugLibUsbApi(
        VendorId,
        ProductId
    );
    private readonly UsbDotNet.Usb _usb;

    public Given_a_fake_hotplug_device_lifecycle(ITestOutputHelper output)
    {
        _loggerFactory = new TestLoggerFactory(output);
        _usb = new UsbDotNet.Usb(
            new LibUsb(_api.Api),
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.None }
        );
        try
        {
            _usb.Initialize();
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [Fact]
    public void A_duplicate_arrival_of_the_same_device_is_ignored()
    {
        var provider = (IHotplugProvider)_usb;
        var arrivals = 0;
        string? leftKey = null;
        provider.DeviceArrived += (_, _) => arrivals++;
        provider.DeviceLeft += (_, d) => leftKey = d.DeviceKey;
        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.Success);

        // With LIBUSB_HOTPLUG_ENUMERATE libusb may notify the arrival of the same device twice
        // (once from registration enumeration, once from the live event loop).
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED);
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED);

        arrivals.Should().Be(1, because: "the duplicate arrival notification must be deduplicated");

        // The device must still be tracked by the first arrival's cached descriptor.
        _api.BusNumber = 0;
        _api.DeviceAddress = 0;
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT);
        leftKey.Should().Be(UsbDeviceDescriptor.GetKey(VendorId, ProductId, 3, 17));
    }

    [Fact]
    public void DeviceLeft_reports_the_key_captured_on_arrival_even_when_bus_address_change()
    {
        var provider = (IHotplugProvider)_usb;
        string? arrivedKey = null;
        string? leftKey = null;
        provider.DeviceArrived += (_, d) => arrivedKey = d.DeviceKey;
        provider.DeviceLeft += (_, d) => leftKey = d.DeviceKey;
        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.Success);

        // Arrival while the device is present: bus 3, address 17.
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED);

        // The device has now left; libusb no longer guarantees bus/address reads. Simulate them
        // returning stale/garbage values.
        _api.BusNumber = 0;
        _api.DeviceAddress = 0;
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT);

        var expected = UsbDeviceDescriptor.GetKey(VendorId, ProductId, 3, 17);
        arrivedKey.Should().Be(expected);
        leftKey
            .Should()
            .Be(
                expected,
                because: "DEVICE_LEFT must report the descriptor cached on arrival, not a stale read"
            );
    }

    [Fact]
    public void DeviceLeft_without_a_prior_arrival_is_dropped()
    {
        var provider = (IHotplugProvider)_usb;
        string? leftKey = null;
        provider.DeviceLeft += (_, d) => leftKey = d.DeviceKey;
        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.Success);

        // No arrival was cached for this device, so no DeviceArrived was ever emitted. A DeviceKey
        // built now would carry unreliable bus number/address, so the event must be dropped.
        Raise(libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT);

        leftKey
            .Should()
            .BeNull(
                because: "a removal without a prior arrival must not be delivered to consumers"
            );
    }

    [Fact]
    public async Task Dispose_does_not_hold_the_instance_lock_while_waiting_for_the_event_loop()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var provider = (IHotplugProvider)_usb;
        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.Success);

        // Hold the event-loop thread inside libusb_handle_events_completed, simulating a thread
        // that cannot exit yet (in the real deadlock: blocked dispatching a hotplug event to a
        // UsbHotplugMonitor whose lock is held by a thread waiting on the Usb instance lock).
        _api.BlockEventLoop = true;
        _api.EventLoopEntered.Wait(timeout).Should().BeTrue();

        var dispose = Task.Run(_usb.Dispose);
        // Dispose has deregistered the callback once the fake clears it; it then waits for the
        // event-loop thread, which the fake is holding until EventLoopRelease is set.
        SpinWait.SpinUntil(() => _api.LastCallback is null, timeout).Should().BeTrue();

        // While Dispose waits for the event loop, another thread must still be able to take the
        // instance lock (e.g. a UsbHotplugMonitor detaching its handlers during its own dispose).
        EventHandler<IUsbDeviceDescriptor> noop = (_, _) => { };
        var detach = Task.Run(() => provider.DeviceArrived -= noop);
        var completed = await Task.WhenAny(detach, Task.Delay(timeout));
        completed
            .Should()
            .Be(detach, because: "the instance lock must not be held while joining the event loop");

        dispose.IsCompleted.Should().BeFalse(because: "the event-loop thread is still held");
        _api.EventLoopRelease.Set();
        await dispose.WaitAsync(timeout);
    }

    private void Raise(libusb_hotplug_event eventType)
    {
        _api.LastCallback.Should().NotBeNull();
        _ = _api.LastCallback!.Invoke(IntPtr.Zero, DevicePtr, eventType, IntPtr.Zero);
    }

    public void Dispose()
    {
        // Release a held event-loop thread so a failed test cannot hang Usb.Dispose below.
        _api.EventLoopRelease.Set();
        _usb.Dispose();
        _api.Dispose();
        _loggerFactory.Dispose();
    }
}
