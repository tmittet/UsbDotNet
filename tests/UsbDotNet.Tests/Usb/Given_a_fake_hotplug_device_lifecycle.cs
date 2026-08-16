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
        var listener = new TestHotplugListener
        {
            DeviceArrived = _ => arrivals++,
            DeviceLeft = d => leftKey = d.DeviceKey,
        };
        provider.RegisterHotplug(listener).Should().Be(HotplugRegistrationResult.Success);

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
        var listener = new TestHotplugListener
        {
            DeviceArrived = d => arrivedKey = d.DeviceKey,
            DeviceLeft = d => leftKey = d.DeviceKey,
        };
        provider.RegisterHotplug(listener).Should().Be(HotplugRegistrationResult.Success);

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
        var listener = new TestHotplugListener { DeviceLeft = d => leftKey = d.DeviceKey };
        provider.RegisterHotplug(listener).Should().Be(HotplugRegistrationResult.Success);

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
        provider
            .RegisterHotplug(new TestHotplugListener())
            .Should()
            .Be(HotplugRegistrationResult.Success);

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
        // instance lock (e.g. a hotplug consumer calling in during shutdown). The call throws
        // ObjectDisposedException once it has the lock; acquiring it is the point.
        var lockProbe = Task.Run(() =>
        {
            try
            {
                _ = provider.RegisterHotplug(new TestHotplugListener());
            }
            catch (ObjectDisposedException)
            {
                // Expected: the instance is disposing.
            }
        });
        var completed = await Task.WhenAny(lockProbe, Task.Delay(timeout));
        completed
            .Should()
            .Be(
                lockProbe,
                because: "the instance lock must not be held while joining the event loop"
            );

        dispose.IsCompleted.Should().BeFalse(because: "the event-loop thread is still held");
        _api.EventLoopRelease.Set();
        await dispose.WaitAsync(timeout);
    }

    [Fact]
    public void Dispose_called_from_a_DeviceArrived_handler_on_the_event_loop_thread_throws_instead_of_deadlocking()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var provider = (IHotplugProvider)_usb;
        using var handlerReturned = new ManualResetEventSlim(false);
        Exception? caught = null;

        var listener = new TestHotplugListener
        {
            DeviceArrived = _ =>
            {
                // This callback runs synchronously on the event-loop thread (see
                // RunOnNextHandleEventsCompleted below). Usb.Dispose() would try to join that same
                // thread; instead of hanging in a self-join, it must fail fast.
                try
                {
                    _usb.Dispose();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    handlerReturned.Set();
                }
            },
        };
        provider.RegisterHotplug(listener).Should().Be(HotplugRegistrationResult.Success);

        // Have the real event-loop thread invoke the hotplug callback itself, from inside
        // libusb_handle_events_completed, exactly as real libusb dispatches a pending event.
        _api.RunOnNextHandleEventsCompleted(() =>
            _api.LastCallback!.Invoke(
                IntPtr.Zero,
                DevicePtr,
                libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED,
                IntPtr.Zero
            )
        );

        handlerReturned
            .Wait(timeout)
            .Should()
            .BeTrue(
                because: "Dispose called from the event-loop thread must not self-join and hang"
            );
        caught.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DeregisterHotplug_waits_for_an_in_flight_event()
    {
        var timeout = TimeSpan.FromSeconds(5);
        var provider = (IHotplugProvider)_usb;
        using var handlerEntered = new ManualResetEventSlim(false);
        using var handlerRelease = new ManualResetEventSlim(false);
        var listener = new TestHotplugListener
        {
            DeviceArrived = _ =>
            {
                handlerEntered.Set();
                handlerRelease.Wait();
            },
        };
        provider.RegisterHotplug(listener).Should().Be(HotplugRegistrationResult.Success);

        // Have the event-loop thread dispatch an arrival that blocks inside the listener.
        _api.RunOnNextHandleEventsCompleted(() =>
            _api.LastCallback!.Invoke(
                IntPtr.Zero,
                DevicePtr,
                libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED,
                IntPtr.Zero
            )
        );
        handlerEntered.Wait(timeout).Should().BeTrue();
        try
        {
            var deregister = Task.Run(() => provider.DeregisterHotplug(listener));

            // DeregisterHotplug promises the listener is never invoked after it returns, so it
            // must block until the in-flight invocation (holding the dispatch lock) completes.
            var completedEarly = await Task.WhenAny(deregister, Task.Delay(250));
            completedEarly
                .Should()
                .NotBe(
                    deregister,
                    because: "DeregisterHotplug must wait for the in-flight listener invocation"
                );

            handlerRelease.Set();
            await deregister.WaitAsync(timeout);
        }
        finally
        {
            // Release the gate so a failed assertion cannot hang the event loop and teardown.
            handlerRelease.Set();
        }
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
