using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_hotplug_monitor_over_a_fake_provider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Dispose_deregisters_the_registration_it_owns()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();

        monitor.Dispose();

        A.CallTo(() => provider.DeregisterHotplug(monitor)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Dispose_of_a_never_subscribed_monitor_does_not_deregister()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);

        monitor.Dispose();

        A.CallTo(() => provider.DeregisterHotplug(A<IHotplugListener>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_hotplug_callback_after_Dispose_is_ignored()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        monitor.Dispose();

        // The provider promises not to invoke the listener after DeregisterHotplug returns, but
        // the monitor keeps its _disposed guard as defense in depth (e.g. against a provider
        // that breaks that promise); a disposed monitor must drop the event.
        RaiseLeft(monitor, new UsbDeviceDescriptor { DeviceKey = "fake-device", BcdUsb = 0x0200 });

        (await subscription.Reader.WaitToReadAsync(cts.Token))
            .Should()
            .BeFalse(because: "a disposed monitor must complete, not write to, its subscriptions");
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
        RaiseArrived(monitor, new UsbDeviceDescriptor { DeviceKey = "zeroed-device" });
        RaiseArrived(
            monitor,
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
            monitor,
            new UsbDeviceDescriptor { DeviceKey = "real-device", BcdUsb = 0x0200 }
        );
        var connected = await subscription.Reader.ReadAsync(cts.Token);
        connected.Type.Should().Be(UsbHotplugEventType.Connected);

        RaiseDisposed(monitor);

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
            monitor,
            new UsbDeviceDescriptor { DeviceKey = "ghost-device", BcdUsb = 0x0200 }
        );
        _ = await subscription.Reader.ReadAsync(cts.Token);

        RaiseDisposed(monitor);

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
        A.CallTo(() => provider.RegisterHotplug(A<IHotplugListener>._))
            .Returns(HotplugRegistrationResult.Success);
        return provider;
    }

    // The monitor registers itself as the provider's IHotplugListener on first Subscribe; tests
    // invoke the listener directly to simulate the provider's libusb event loop thread.
    private static void RaiseArrived(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceArrived(descriptor);

    private static void RaiseLeft(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceLeft(descriptor);

    /// <summary>Simulates the underlying Usb instance completing its Dispose.</summary>
    private static void RaiseDisposed(UsbHotplugMonitor monitor) =>
        ((IHotplugListener)monitor).OnProviderDisposed();
}
