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
        // that breaks that promise); a disposed monitor must drop the event. Had the event been
        // written, WaitToReadAsync would return true instead of surfacing the cancellation.
        RaiseLeft(monitor, new UsbDeviceDescriptor { DeviceKey = "fake-device", BcdUsb = 0x0200 });

        var wait = async () => await subscription.Reader.WaitToReadAsync(cts.Token);
        (
            await wait.Should()
                .ThrowAsync<OperationCanceledException>(
                    because: "a disposed monitor must cancel, not write to, its subscriptions"
                )
        )
            .Which.CancellationToken.Should()
            .NotBe(cts.Token, because: "the abort must come from the monitor, not the timeout");
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
    public async Task When_the_provider_is_disposed_subscription_readers_are_canceled()
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

        // This event is never read before the teardown below; it must be dropped, not delivered.
        RaiseArrived(
            monitor,
            new UsbDeviceDescriptor { DeviceKey = "stale-device", BcdUsb = 0x0200 }
        );

        RaiseDisposed(monitor);

        // The channel is canceled and undelivered events are dropped, so a consumer wakes and
        // observes the teardown it did not initiate instead of a stale event, a subscription that
        // stays silent forever, or a clean end-of-stream it cannot tell apart from a normal stop.
        var wait = async () => await subscription.Reader.WaitToReadAsync(cts.Token);
        (
            await wait.Should()
                .ThrowAsync<OperationCanceledException>()
                .WithMessage("*IUsb instance is disposed*")
        )
            .Which.CancellationToken.Should()
            .NotBe(cts.Token, because: "the cancellation must come from teardown, not the timeout");
        subscription.Reader.Completion.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task Disposing_the_monitor_wakes_a_blocked_reader_with_a_cancellation()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        // Block a reader on the empty channel before disposing; Dispose must wake it, and with a
        // cancellation rather than an end-of-stream `false`.
        var blockedRead = subscription.Reader.WaitToReadAsync(cts.Token);

        monitor.Dispose();

        OperationCanceledException? canceled = null;
        try
        {
            _ = await blockedRead;
        }
        catch (OperationCanceledException ex)
        {
            canceled = ex;
        }
        canceled
            .Should()
            .NotBeNull(because: "Dispose must wake the blocked reader with a cancellation");
        canceled!
            .CancellationToken.Should()
            .NotBe(cts.Token, because: "the wake-up must come from Dispose, not the timeout");
        subscription.Reader.Completion.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task Disposing_the_monitor_drops_buffered_events()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        RaiseArrived(
            monitor,
            new UsbDeviceDescriptor { DeviceKey = "stale-device", BcdUsb = 0x0200 }
        );

        monitor.Dispose();

        // Undelivered events describe devices the disposed monitor no longer tracks; they are
        // dropped, so a reader that is behind observes the cancellation instead of acting on a
        // stale event.
        var read = async () => await subscription.Reader.ReadAsync(cts.Token);
        await read.Should().ThrowAsync<OperationCanceledException>();
        subscription.Reader.Completion.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task Disposing_a_subscription_completes_its_reader_cleanly()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        var subscription = monitor.Subscribe();
        using var cts = new CancellationTokenSource(Timeout);

        // Self-dispose is the consumer's own stop request, so unlike monitor or provider
        // disposal it must end the stream cleanly, not with a cancellation.
        subscription.Dispose();

        (await subscription.Reader.WaitToReadAsync(cts.Token)).Should().BeFalse();
        subscription.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
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

    [Fact]
    public void Subscribing_while_the_provider_is_disposing_throws_InvalidOperationException()
    {
        // Usb signals Disposing at the start of its teardown but raises OnProviderDisposed only
        // at the end; in that window RegisterHotplug throws ObjectDisposedException. The monitor
        // must surface its documented contract (InvalidOperationException for a dead provider),
        // reserving ObjectDisposedException for a disposed monitor.
        var provider = A.Fake<IHotplugProvider>();
        A.CallTo(() => provider.IsHotplugSupported).Returns(true);
        A.CallTo(() => provider.RegisterHotplug(A<IHotplugListener>._))
            .Throws(new ObjectDisposedException(nameof(IHotplugProvider)));
        using var monitor = new UsbHotplugMonitor(provider);

        FluentActions
            .Invoking(() => monitor.Subscribe())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*IUsb instance is disposed*")
            .WithInnerException<ObjectDisposedException>();

        // Subsequent attempts fail fast with the same contract, without touching the disposed
        // provider again.
        FluentActions
            .Invoking(() => monitor.Subscribe())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*IUsb instance is disposed*");
        A.CallTo(() => provider.RegisterHotplug(A<IHotplugListener>._))
            .MustHaveHappenedOnceExactly();
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
