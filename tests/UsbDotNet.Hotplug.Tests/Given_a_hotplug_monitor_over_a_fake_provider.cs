using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_hotplug_monitor_over_a_fake_provider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Dispose_does_not_hold_the_monitor_lock_while_detaching_from_the_provider()
    {
        using var detachEntered = new ManualResetEventSlim(false);
        using var detachRelease = new ManualResetEventSlim(false);
        var provider = CreateFakeProvider();
        // Clearing the DeviceArrived callback (assigning null) signals detachEntered and blocks
        // until detachRelease is set, simulating a detach that is stuck waiting for the Usb
        // instance lock. The non-null assignment made by Subscribe is left unconfigured so the
        // fake's property behavior records it.
        A.CallTo(provider)
            .Where(call =>
                call.Method.Name == "set_DeviceArrived"
                && call.Arguments.Get<Action<IUsbDeviceDescriptor>>(0) == null
            )
            .Invokes(() =>
            {
                detachEntered.Set();
                detachRelease.Wait();
            });
        // Also disposed via the Task.Run below; Dispose is idempotent so the scope-exit Dispose
        // here (which satisfies CA2000) is harmless.
        using var monitor = new UsbHotplugMonitor(provider);
        // Declared before the try so its scope-exit Dispose (which takes the monitor lock) runs
        // after the finally below has released the detach gate, even when an assertion fails.
        using var subscription = monitor.Subscribe();
        try
        {
            var dispose = Task.Run(monitor.Dispose);
            detachEntered.Wait(Timeout).Should().BeTrue();

            // While Dispose is detaching from the provider, the libusb event-loop thread must be
            // able to run Dispatch (and have the event ignored) instead of blocking on the monitor
            // lock; blocking here is one edge of a shutdown deadlock with a disposing Usb instance.
            var dispatch = Task.Run(() =>
                RaiseLeft(
                    provider,
                    new UsbDeviceDescriptor { DeviceKey = "fake-device", BcdUsb = 0x0200 }
                )
            );
            var completed = await Task.WhenAny(dispatch, Task.Delay(Timeout));
            completed
                .Should()
                .Be(
                    dispatch,
                    because: "the monitor lock must not be held while detaching provider events"
                );

            detachRelease.Set();
            await dispose.WaitAsync(Timeout);
        }
        finally
        {
            // Release the detach gate so a failed assertion cannot hang the scope-exit Dispose.
            detachRelease.Set();
        }
    }

    [Fact]
    public async Task A_subscriber_never_receives_a_device_with_a_zeroed_descriptor()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        // The Windows backend synthesizes descriptors with BcdUsb == 0 for root hubs and for
        // devices whose real descriptor could not be read; no UsbDeviceFilter matches them, so
        // they must not reach subscribers or be tracked for late-subscriber replay.
        RaiseArrived(provider, new UsbDeviceDescriptor { DeviceKey = "zeroed-device" });
        RaiseArrived(
            provider,
            new UsbDeviceDescriptor { DeviceKey = "real-device", BcdUsb = 0x0200 }
        );

        var live = await subscription.Reader.ReadAsync(cts.Token);
        live.Descriptor.DeviceKey.Should().Be("real-device");

        // A late subscriber receives the tracked connected devices; the zeroed device must not
        // have been tracked.
        using var late = monitor.Subscribe();
        var replayed = await late.Reader.ReadAsync(cts.Token);
        replayed.Descriptor.DeviceKey.Should().Be("real-device");
    }

    [Fact]
    public async Task When_the_provider_is_disposed_subscriptions_complete_cleanly()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        RaiseArrived(
            provider,
            new UsbDeviceDescriptor { DeviceKey = "real-device", BcdUsb = 0x0200 }
        );
        var connected = await subscription.Reader.ReadAsync(cts.Token);
        connected.Type.Should().Be(UsbHotplugEventType.Connected);

        RaiseDisposed(provider);

        // The channel completes, so a consumer observes a clean end-of-stream instead of a
        // subscription that stays silent forever on a provider that no longer exists.
        (await subscription.Reader.WaitToReadAsync(cts.Token))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Subscribing_after_the_provider_is_disposed_throws_instead_of_replaying_stale_devices()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        RaiseArrived(
            provider,
            new UsbDeviceDescriptor { DeviceKey = "ghost-device", BcdUsb = 0x0200 }
        );
        _ = await subscription.Reader.ReadAsync(cts.Token);

        RaiseDisposed(provider);

        // Without this, a late subscriber would get a Connected replay of "ghost-device" from the
        // frozen snapshot and then act on a device the disposed IUsb can no longer reach.
        FluentActions
            .Invoking(() => monitor.Subscribe())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*IUsb instance is disposed*");
    }

    private static IHotplugProvider CreateFakeProvider()
    {
        var provider = A.Fake<IHotplugProvider>();
        A.CallTo(() => provider.IsHotplugSupported).Returns(true);
        A.CallTo(() => provider.RegisterHotplug()).Returns(HotplugRegistrationResult.Success);
        return provider;
    }

    // The monitor assigns its callbacks on first Subscribe; the fake's property behavior records
    // them, so tests invoke the recorded callback to simulate the libusb event loop thread.
    private static void RaiseArrived(IHotplugProvider provider, UsbDeviceDescriptor descriptor) =>
        provider.DeviceArrived!.Invoke(descriptor);

    private static void RaiseLeft(IHotplugProvider provider, UsbDeviceDescriptor descriptor) =>
        provider.DeviceLeft!.Invoke(descriptor);

    /// <summary>Simulates the underlying Usb instance completing its Dispose.</summary>
    private static void RaiseDisposed(IHotplugProvider provider) => provider.Disposed!.Invoke();
}
