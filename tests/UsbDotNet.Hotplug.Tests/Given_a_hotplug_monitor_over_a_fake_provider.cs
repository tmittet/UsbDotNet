using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_hotplug_monitor_over_a_fake_provider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Dispose_deregisters_the_registration_it_owns()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        // Registration happens on the first read, not on the Subscribe call, so the stream has to
        // be started before there is anything for Dispose to release.
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse(because: "the subscription must be live by now");

        monitor.Dispose();

        A.CallTo(() => provider.DeregisterHotplug(monitor)).MustHaveHappenedOnceExactly();
        // Observe the cancellation: disposing an enumerator with a read still in flight is
        // undefined, so every primed read in this suite is awaited before the scope ends.
        var read = async () => await parked;
        await read.Should().ThrowAsync<OperationCanceledException>();
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
    public void Abandoning_the_stream_without_enumerating_registers_nothing()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);

        _ = monitor.Subscribe();

        // Subscribe is an iterator method, so discarding what it returns runs none of its body:
        // nothing registers with the provider, no channel exists and no event is buffered. The
        // eager design this replaced leaked a live, buffering subscription here until the monitor
        // was disposed, with no analyzer to catch it.
        monitor
            .SubscriptionCount.Should()
            .Be(0, because: "enumeration, not the call, is what joins the fan-out");
        A.CallTo(() => provider.RegisterHotplug(A<IHotplugListener>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_hotplug_callback_after_Dispose_is_ignored()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse(because: "the subscription must be live by now");

        monitor.Dispose();

        // The provider promises not to invoke the listener after DeregisterHotplug returns, but
        // the monitor keeps its _disposed guard as defense in depth (e.g. against a provider
        // that breaks that promise); a disposed monitor must drop the event.
        RaiseArrived(monitor, Device("fake-device"));

        // Asserted separately from the cancellation below: the consumer would observe a
        // cancellation either way (a terminated subscription refuses to yield buffered events),
        // so only the tracked-device count proves the Dispatch guard itself fired.
        monitor
            .ConnectedCount.Should()
            .Be(0, because: "a disposed monitor must drop the event, not track it");
        var read = async () => await parked;
        (
            await read.Should()
                .ThrowAsync<OperationCanceledException>(
                    because: "a disposed monitor must cancel, not write to, its subscriptions"
                )
        )
            .Which.CancellationToken.Should()
            .Be(
                CancellationToken.None,
                because: "the cancellation must come from the monitor, not the timeout"
            );
    }

    [Fact]
    public async Task A_subscriber_never_receives_a_device_with_a_zeroed_descriptor()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        // Primed so the arrivals below travel the live fan-out path rather than being picked up by
        // this subscription's own start-of-enumeration snapshot.
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        // The Windows backend synthesizes descriptors with BcdUsb == 0 for root hubs and for
        // devices whose real descriptor could not be read; no UsbDeviceFilter matches them, so
        // they must not reach subscribers or be tracked for late-subscriber replay.
        RaiseArrived(monitor, new UsbDeviceDescriptor { DeviceKey = "zeroed-device" });
        RaiseArrived(monitor, Device("real-device"));

        (await parked).Should().BeTrue();
        events.Current.Descriptor.DeviceKey.Should().Be("real-device");
        monitor
            .ConnectedCount.Should()
            .Be(1, because: "the zeroed device must not be tracked either");

        // A late subscriber replays the tracked connected devices; the zeroed device is not one.
        await using var late = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        (await late.MoveNextAsync()).Should().BeTrue();
        late.Current.Descriptor.DeviceKey.Should().Be("real-device");
    }

    [Fact]
    public async Task When_the_provider_is_disposed_consumers_are_canceled()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        RaiseArrived(monitor, Device("real-device"));
        (await parked).Should().BeTrue();
        events.Current.Type.Should().Be(UsbHotplugEventType.Connected);

        // This event is never read before the teardown below; it must be dropped, not delivered.
        RaiseArrived(monitor, Device("stale-device"));

        RaiseDisposed(monitor);

        // A terminated subscription refuses to yield the buffered event, so the consumer wakes
        // and observes the teardown it did not initiate instead of a stale event, a subscription
        // that stays silent forever, or an end it cannot tell apart from its own stop. Without the
        // check before each yield the buffered "stale-device" would be delivered here, because
        // WaitToReadAsync reports a non-empty queue as readable regardless of the writer being
        // completed.
        var read = async () => await events.MoveNextAsync();
        (
            await read.Should()
                .ThrowAsync<OperationCanceledException>()
                .WithMessage("*IUsb instance is disposed*")
        )
            .Which.CancellationToken.Should()
            .Be(
                CancellationToken.None,
                because: "the cancellation must come from teardown, not the timeout"
            );
    }

    [Fact]
    public async Task Disposing_the_monitor_wakes_a_parked_consumer_with_a_cancellation()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);

        // The first MoveNextAsync runs the iterator body synchronously — registration, the
        // snapshot, joining the fan-out — and then parks on the empty channel. Dispose must wake
        // it, and with a cancellation rather than an end-of-stream `false`.
        var parked = events.MoveNextAsync();
        parked
            .IsCompleted.Should()
            .BeFalse(
                because: "the consumer must actually be parked, otherwise this only re-tests the "
                    + "buffered-event path"
            );

        monitor.Dispose();

        var read = async () => await parked;
        (
            await read.Should()
                .ThrowAsync<OperationCanceledException>(
                    because: "Dispose must wake the parked consumer with a cancellation"
                )
                .WithMessage("*UsbHotplugMonitor was disposed*")
        )
            .Which.CancellationToken.Should()
            .Be(
                CancellationToken.None,
                because: "the wake-up must come from Dispose, not the timeout"
            );
    }

    [Fact]
    public async Task Disposing_the_monitor_drops_buffered_events()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        // One event read so the stream is past its snapshot and into the channel, then one more
        // buffered and deliberately left unread.
        RaiseArrived(monitor, Device("real-device"));
        (await parked).Should().BeTrue();
        RaiseArrived(monitor, Device("stale-device"));

        monitor.Dispose();

        // Undelivered events describe devices the disposed monitor no longer tracks; they are
        // refused, so a consumer that is behind observes the cancellation instead of acting on a
        // stale event.
        var read = async () => await events.MoveNextAsync();
        (await read.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(CancellationToken.None);
    }

    [Fact]
    public async Task Disposing_the_monitor_refuses_the_rest_of_the_snapshot()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        RaiseArrived(monitor, Device("device-a"));
        RaiseArrived(monitor, Device("device-b"));

        // The first read starts the subscription and yields the first of the two snapshot devices,
        // leaving the iterator suspended mid-snapshot with the second still pending.
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        (await events.MoveNextAsync()).Should().BeTrue();

        monitor.Dispose();

        // The pending snapshot entry describes a device the disposed monitor no longer tracks, so
        // it must be refused exactly like a buffered channel event. The snapshot is a plain local
        // list rather than a channel, so it needs its own check before each yield — the completed
        // writer cannot speak for it.
        var read = async () => await events.MoveNextAsync();
        (await read.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(CancellationToken.None);
    }

    [Fact]
    public async Task Enumerating_the_returned_stream_twice_creates_two_independent_subscriptions()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        var stream = monitor.Subscribe();
        await using var first = stream.GetAsyncEnumerator(cts.Token);
        await using var second = stream.GetAsyncEnumerator(cts.Token);
        var firstRead = first.MoveNextAsync();
        var secondRead = second.MoveNextAsync();
        firstRead.IsCompleted.Should().BeFalse();
        secondRead.IsCompleted.Should().BeFalse();

        // Each enumeration runs the iterator body afresh and so owns its own channel. There is no
        // shared state to split between them, which is why nothing needs to reject the second
        // enumerator — the eager design this replaced had to.
        monitor
            .SubscriptionCount.Should()
            .Be(2, because: "each enumeration of the same stream value registers separately");

        RaiseArrived(monitor, Device("real-device"));

        (await firstRead).Should().BeTrue();
        (await secondRead).Should().BeTrue();
        first.Current.Descriptor.DeviceKey.Should().Be("real-device");
        second.Current.Descriptor.DeviceKey.Should().Be("real-device");
    }

    [Fact]
    public async Task Starting_a_second_enumeration_after_the_first_ended_registers_again()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        var stream = monitor.Subscribe();
        RaiseArrived(monitor, Device("real-device"));

        await foreach (var e in stream.WithCancellation(cts.Token))
        {
            e.Descriptor.DeviceKey.Should().Be("real-device");
            break;
        }
        monitor.SubscriptionCount.Should().Be(0);

        // The value Subscribe returns is reusable rather than single-use: enumerating it again
        // takes a fresh snapshot through a fresh subscription.
        await foreach (var e in stream.WithCancellation(cts.Token))
        {
            e.Descriptor.DeviceKey.Should().Be("real-device");
            break;
        }
        monitor
            .SubscriptionCount.Should()
            .Be(0, because: "the second enumeration unsubscribes on break just like the first");
    }

    [Fact]
    public async Task Enumeration_snapshots_connected_devices_when_it_starts()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        RaiseArrived(monitor, Device("device-a"));
        var stream = monitor.Subscribe();
        RaiseLeft(monitor, Device("device-a"));

        // The snapshot belongs to the start of enumeration, not to the Subscribe call: device-a
        // had already left by then, so it is neither replayed as Connected nor reported as
        // leaving, and the consumer has nothing to read.
        await using var events = stream.GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked
            .IsCompleted.Should()
            .BeFalse(
                because: "a device that left before enumeration started is not in its snapshot"
            );

        // Prove the subscription is genuinely live rather than merely slow.
        RaiseArrived(monitor, Device("device-b"));
        (await parked).Should().BeTrue();
        events.Current.Type.Should().Be(UsbHotplugEventType.Connected);
        events.Current.Descriptor.DeviceKey.Should().Be("device-b");
    }

    [Fact]
    public async Task Devices_connected_before_enumeration_starts_are_replayed()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        var stream = monitor.Subscribe();
        RaiseArrived(monitor, Device("real-device"));

        // Nothing is lost by subscribing before the device arrives: whatever is connected when
        // enumeration starts is replayed as Connected from the snapshot.
        await using var events = stream.GetAsyncEnumerator(cts.Token);
        (await events.MoveNextAsync()).Should().BeTrue();
        events.Current.Type.Should().Be(UsbHotplugEventType.Connected);
        events.Current.Descriptor.DeviceKey.Should().Be("real-device");
    }

    [Fact]
    public async Task The_callers_token_cancels_a_parked_consumer()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource();
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);

        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        await cts.CancelAsync();

        // Bounded rather than a bare await: if the caller's token is not plumbed into the
        // iterator the read stays parked forever, and a hung run is a far worse failure than a
        // TimeoutException. The token is the only thing that carries a consumer's own
        // cancellation, and dropping it is invisible at both build and review time.
        var read = async () => await parked.AsTask().WaitAsync(Timeout, CancellationToken.None);
        (await read.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(cts.Token, because: "the consumer's own cancellation is identified by its token");
    }

    [Fact]
    public async Task WithCancellation_ends_an_await_foreach()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource();
        var stream = monitor.Subscribe();

        await cts.CancelAsync();

        // The same guarantee as the test above, through the syntax consumers actually write:
        // WithCancellation reaches the iterator via GetAsyncEnumerator, a different code path
        // from passing the token to GetAsyncEnumerator directly.
        await Consuming(stream, cts.Token).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Breaking_out_of_the_stream_unsubscribes()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        var stream = monitor.Subscribe();
        monitor
            .SubscriptionCount.Should()
            .Be(0, because: "an iterator method registers nothing until enumeration starts");
        RaiseArrived(monitor, Device("real-device"));

        await foreach (var e in stream.WithCancellation(cts.Token))
        {
            monitor
                .SubscriptionCount.Should()
                .Be(1, because: "enumeration is what joins the fan-out");
            e.Descriptor.DeviceKey.Should().Be("real-device");
            break;
        }

        monitor
            .SubscriptionCount.Should()
            .Be(
                0,
                because: "await foreach disposes the enumerator on break, which unsubscribes; "
                    + "this replaces the old explicit subscription Dispose"
            );
    }

    [Fact]
    public async Task Cancelling_the_callers_token_unsubscribes()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource();

        var stream = monitor.Subscribe();
        await cts.CancelAsync();
        await Consuming(stream, cts.Token).Should().ThrowAsync<OperationCanceledException>();

        monitor
            .SubscriptionCount.Should()
            .Be(
                0,
                because: "the enumerator's finally runs on a cancellation too, not only on break"
            );
    }

    [Fact]
    public async Task Every_live_subscription_is_terminated_on_dispose()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);

        var consumers = Enumerable
            .Range(0, 3)
            .Select(_ => monitor.Subscribe().GetAsyncEnumerator(cts.Token))
            .ToList();
        // The MoveNextAsync calls are what register the three subscriptions; without them Dispose
        // would have nothing to terminate.
        var parked = consumers.Select(c => c.MoveNextAsync()).ToList();
        monitor.SubscriptionCount.Should().Be(3);

        monitor.Dispose();

        // Each consumer throws its own exception instance, which is why the reason is rebuilt per
        // throw site rather than shared.
        foreach (var read in parked)
        {
            var awaiting = async () => await read;
            (await awaiting.Should().ThrowAsync<OperationCanceledException>())
                .Which.CancellationToken.Should()
                .Be(CancellationToken.None);
        }
        foreach (var consumer in consumers)
        {
            await consumer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Two_consumers_each_receive_the_event()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var first = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        await using var second = monitor.Subscribe().GetAsyncEnumerator(cts.Token);

        // Both primed before the arrival, so this exercises the Dispatch fan-out rather than two
        // independent start-of-enumeration snapshots.
        var firstRead = first.MoveNextAsync();
        var secondRead = second.MoveNextAsync();
        firstRead.IsCompleted.Should().BeFalse();
        secondRead.IsCompleted.Should().BeFalse();

        RaiseArrived(monitor, Device("real-device"));

        (await firstRead).Should().BeTrue();
        (await secondRead).Should().BeTrue();
        first.Current.Descriptor.DeviceKey.Should().Be("real-device");
        second
            .Current.Descriptor.DeviceKey.Should()
            .Be("real-device", because: "each subscription gets its own copy of a matching event");
    }

    [Fact]
    public async Task A_filtered_consumer_only_receives_matching_devices()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor
            .Subscribe(new UsbDeviceFilter(VendorIds: [0x1234]))
            .GetAsyncEnumerator(cts.Token);

        // Primed, so the filter is exercised where Dispatch applies it per event rather than where
        // Subscribe applies it to the snapshot.
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        RaiseArrived(monitor, Device("other-vendor", vendorId: 0x9999));
        RaiseArrived(monitor, Device("matching-vendor"));

        (await parked).Should().BeTrue();
        events
            .Current.Descriptor.DeviceKey.Should()
            .Be(
                "matching-vendor",
                because: "a non-matching device is never written to this subscription, so it "
                    + "cannot be the first event read"
            );
    }

    [Fact]
    public async Task Concurrent_Subscribe_and_Dispose_never_hang_a_consumer()
    {
        var unobserved = new List<Exception>();
        void OnUnobserved(object? _, UnobservedTaskExceptionEventArgs e)
        {
            lock (unobserved)
            {
                unobserved.Add(e.Exception);
            }
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            // A subscription added after Dispose took its snapshot would never be terminated and
            // its consumer would park forever; the monitor's lock discipline closes that window,
            // and only racing the two operations exercises it.
            for (var i = 0; i < 500; i++)
            {
                var provider = CreateFakeProvider();
                // Disposed by the racing task below; the redundant dispose here is a harmless
                // no-op that keeps the monitor's lifetime visibly scoped to one iteration.
                using var monitor = new UsbHotplugMonitor(provider);

                var consume = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await foreach (var _ in monitor.Subscribe()) { }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Dispose won the race to the first read.
                        }
                        catch (OperationCanceledException)
                        {
                            // Dispose won the race to a live subscription.
                        }
                    },
                    CancellationToken.None
                );
                var dispose = Task.Run(monitor.Dispose, CancellationToken.None);

                // Bounded so a missed termination fails the test instead of hanging the run.
                await Task.WhenAll(consume, dispose).WaitAsync(Timeout, CancellationToken.None);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }

        // Unobserved exceptions surface only when the faulted task is finalized.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        unobserved.Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribing_after_the_provider_is_disposed_throws_instead_of_replaying_stale_devices()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = events.MoveNextAsync();
        parked.IsCompleted.Should().BeFalse();

        RaiseArrived(monitor, Device("ghost-device"));
        (await parked).Should().BeTrue();

        RaiseDisposed(monitor);

        // Without this, a late subscriber would get a Connected replay of "ghost-device" from the
        // frozen snapshot and then act on a device the disposed IUsb can no longer reach. The
        // throw lands on the first read rather than on the Subscribe call, because Subscribe is an
        // iterator method and defers its whole body to then.
        (
            await FirstReadOf(monitor.Subscribe(), cts.Token)
                .Should()
                .ThrowAsync<InvalidOperationException>()
        ).WithMessage("*IUsb instance is disposed*");
    }

    [Fact]
    public async Task Subscribing_while_the_provider_is_disposing_throws_InvalidOperationException()
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
        using var cts = new CancellationTokenSource(Timeout);

        (
            await FirstReadOf(monitor.Subscribe(), cts.Token)
                .Should()
                .ThrowAsync<InvalidOperationException>()
        )
            .WithMessage("*IUsb instance is disposed*")
            .WithInnerException<ObjectDisposedException>();

        // Subsequent attempts fail fast with the same contract, without touching the disposed
        // provider again: the latched _providerDisposed flag short-circuits EnsureRegistered.
        (
            await FirstReadOf(monitor.Subscribe(), cts.Token)
                .Should()
                .ThrowAsync<InvalidOperationException>()
        ).WithMessage("*IUsb instance is disposed*");
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

    private static UsbDeviceDescriptor Device(string key, ushort vendorId = 0x1234) =>
        new()
        {
            DeviceKey = key,
            BcdUsb = 0x0200,
            VendorId = vendorId,
        };

    /// <summary>
    /// Enumerates a stream to completion on a worker thread, bounded by the test timeout.
    /// Enumerating a live subscription never ends on its own, so an assertion that the stream
    /// ends must fail on a timeout rather than hang the whole run.
    /// </summary>
    private static Func<Task> Consuming(
        IAsyncEnumerable<UsbHotplugEvent> stream,
        CancellationToken token
    ) =>
        async () =>
            await Task.Run(
                    async () =>
                    {
                        await foreach (var _ in stream.WithCancellation(token)) { }
                    },
                    CancellationToken.None
                )
                .WaitAsync(Timeout, CancellationToken.None);

    /// <summary>
    /// Starts enumerating and awaits only the first read. Subscribe is an iterator method, so the
    /// exceptions it documents surface here rather than at the call, and this is where a test has
    /// to look for them.
    /// </summary>
    private static Func<Task> FirstReadOf(
        IAsyncEnumerable<UsbHotplugEvent> stream,
        CancellationToken token
    ) =>
        async () =>
        {
            await using var events = stream.GetAsyncEnumerator(token);
            _ = await events.MoveNextAsync();
        };

    // The monitor registers itself as the provider's IHotplugListener on the first read of a
    // subscription; tests invoke the listener directly to simulate the provider's libusb event
    // loop thread.
    private static void RaiseArrived(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceArrived(descriptor);

    private static void RaiseLeft(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceLeft(descriptor);

    /// <summary>Simulates the underlying Usb instance completing its Dispose.</summary>
    private static void RaiseDisposed(UsbHotplugMonitor monitor) =>
        ((IHotplugListener)monitor).OnProviderDisposed();
}
