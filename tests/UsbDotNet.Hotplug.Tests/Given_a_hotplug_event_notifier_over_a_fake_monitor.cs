using System.Runtime.CompilerServices;
using FakeItEasy;
using UsbDotNet.Descriptor;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_hotplug_event_notifier_over_a_fake_monitor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DeviceConnected_is_raised_for_each_connected_event()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"), Connected("device-b"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        notifier.DeviceConnected += (_, e) => seen.Add(e.Descriptor.DeviceKey);

        await notifier.RunAsync(CancellationToken.None);

        seen.Should().Equal("device-a", "device-b");
    }

    [Fact]
    public async Task Each_event_type_reaches_only_its_own_handler()
    {
        using var monitor = CreateFakeMonitor(Connected("arrived"), Disconnected("left"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var connected = new List<string>();
        var disconnected = new List<string>();
        notifier.DeviceConnected += (_, e) => connected.Add(e.Descriptor.DeviceKey);
        notifier.DeviceDisconnected += (_, e) => disconnected.Add(e.Descriptor.DeviceKey);

        await notifier.RunAsync(CancellationToken.None);

        connected.Should().Equal("arrived");
        disconnected.Should().Equal("left");
    }

    [Fact]
    public async Task A_handler_attached_after_RunAsync_starts_misses_the_initial_burst()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        // RunAsync runs synchronously up to its first incomplete await, and an already-buffered
        // snapshot is delivered inside that window — so by the time the call returns its Task the
        // burst is gone. This is what makes "attach handlers, then RunAsync" the contract; the
        // point of the new design is that there is nothing to call before attaching, so the
        // ordering is hard to get wrong rather than merely documented.
        var run = notifier.RunAsync(CancellationToken.None);
        var late = new List<string>();
        notifier.DeviceConnected += (_, e) => late.Add(e.Descriptor.DeviceKey);
        await run;

        late.Should().BeEmpty(because: "the burst was delivered before the handler was attached");
    }

    [Fact]
    public async Task A_throwing_handler_does_not_stop_the_others()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var reachedSecondHandler = false;
        notifier.DeviceConnected += (_, _) => throw new InvalidOperationException("boom");
        notifier.DeviceConnected += (_, _) => reachedSecondHandler = true;

        // Must not fault: each handler is invoked individually and a thrower is logged, because
        // isolation between handlers is most of the reason to expose an event at all.
        await notifier.RunAsync(CancellationToken.None);

        reachedSecondHandler
            .Should()
            .BeTrue(because: "one throwing handler must not deny the event to the others");
    }

    [Fact]
    public async Task Running_the_same_notifier_twice_throws()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        await notifier.RunAsync(CancellationToken.None);

        // The stream itself is legitimately reusable, but a second run over the same handlers would
        // raise every event twice, which is a bug rather than a feature.
        var second = async () => await notifier.RunAsync(CancellationToken.None);
        (await second.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "*already been run*"
        );
        A.CallTo(() => monitor.Subscribe(A<IUsbDeviceFilter?>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Cancelling_the_token_ends_RunAsync()
    {
        using var monitor = A.Fake<IUsbHotplugMonitor>();
        A.CallTo(() => monitor.Subscribe(A<IUsbDeviceFilter?>._, A<CancellationToken>._))
            .ReturnsLazily((IUsbDeviceFilter? _, CancellationToken token) => NeverEnding(token));
        using var cts = new CancellationTokenSource();
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        var run = notifier.RunAsync(cts.Token);
        await cts.CancelAsync();

        // Bounded rather than a bare await: if the token is not forwarded to the monitor the run
        // never ends, and a hung test run is a far worse failure than a TimeoutException.
        var awaiting = async () => await run.WaitAsync(Timeout, CancellationToken.None);
        (await awaiting.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(cts.Token, because: "the consumer's own cancellation is identified by its token");
    }

    [Fact]
    public async Task RunAsync_forwards_the_filter_and_the_token_to_the_monitor()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x1234]);
        using var monitor = CreateFakeMonitor();
        using var cts = new CancellationTokenSource(Timeout);
        await using var notifier = new UsbHotplugEventNotifier(monitor, filter);

        await notifier.RunAsync(cts.Token);

        A.CallTo(() => monitor.Subscribe(filter, cts.Token)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Start_raises_the_initial_burst_to_handlers_attached_first()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"), Connected("device-b"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        notifier.DeviceConnected += (_, e) => seen.Add(e.Descriptor.DeviceKey);

        notifier.Start();

        // Asserted without awaiting anything: Start runs the subscription on this thread, so a
        // buffered burst is already delivered by the time it returns. That is why it is not a
        // Task.Run.
        seen.Should().Equal("device-a", "device-b");
    }

    [Fact]
    public async Task A_handler_attached_after_Start_misses_the_initial_burst()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();
        var late = new List<string>();
        notifier.DeviceConnected += (_, e) => late.Add(e.Descriptor.DeviceKey);

        late.Should().BeEmpty(because: "the burst was delivered before the handler was attached");
    }

    [Fact]
    public async Task Starting_twice_throws()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();

        // Synchronously, unlike RunAsync's faulted task: a void method has no other way to report.
        var second = () => notifier.Start();
        second.Should().Throw<InvalidOperationException>().WithMessage("*already been run*");
    }

    [Fact]
    public async Task RunAsync_after_Start_throws()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();

        var run = async () => await notifier.RunAsync(CancellationToken.None);
        _ = await run.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Start_after_RunAsync_throws()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        await notifier.RunAsync(CancellationToken.None);

        var start = () => notifier.Start();
        start.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Start_after_disposal_throws()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        await notifier.DisposeAsync();

        var start = () => notifier.Start();
        start.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Start_forwards_the_filter_to_the_monitor()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x1234]);
        using var monitor = CreateFakeMonitor();
        await using var notifier = new UsbHotplugEventNotifier(monitor, filter);

        notifier.Start();

        A.CallTo(() => monitor.Subscribe(filter, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Cancelling_the_token_passed_to_Start_ends_the_subscription()
    {
        using var monitor = CreateNeverEndingMonitor();
        using var cts = new CancellationTokenSource();
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start(cts.Token);
        await cts.CancelAsync();

        // Disposal is what observes the end, and it must not surface the cancellation the caller
        // asked for. Bounded, so a token that never reached the monitor fails instead of hanging.
        var disposing = async () =>
            await notifier.DisposeAsync().AsTask().WaitAsync(Timeout, CancellationToken.None);
        await disposing.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Disposal_stops_a_live_subscription()
    {
        using var monitor = CreateNeverEndingMonitor();
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();

        // Disposal has to end a subscription parked on an empty stream without the caller holding
        // a token of their own — that is the whole point of Start over RunAsync.
        var disposing = async () =>
            await notifier.DisposeAsync().AsTask().WaitAsync(Timeout, CancellationToken.None);
        await disposing.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_event_read_after_disposal_is_not_delivered()
    {
        var gate = new TaskCompletionSource();
        using var monitor = CreateGatedMonitor(
            [Connected("before")],
            gate.Task,
            [Connected("after")]
        );
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        notifier.DeviceConnected += (_, e) => seen.Add(e.Descriptor.DeviceKey);

        notifier.Start();
        var disposing = notifier.DisposeAsync().AsTask();
        gate.SetResult();
        await disposing.WaitAsync(Timeout, CancellationToken.None);

        // "after" is already in the stream and gets read while the loop unwinds, so cancellation
        // alone would still deliver it. Only a check before dispatch keeps it from the handler.
        seen.Should().Equal("before");
    }

    [Fact]
    public async Task Disposal_rethrows_a_subscription_failure()
    {
        var gate = new TaskCompletionSource();
        var failure = new InvalidOperationException("the subscription broke");
        using var monitor = CreateGatedMonitor([], gate.Task, [], failure);
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();
        var disposing = notifier.DisposeAsync().AsTask();
        gate.SetResult();

        // Not swallowed the way our own cancellation is: nothing else is left to tell the caller
        // that the subscription died.
        var awaiting = async () => await disposing.WaitAsync(Timeout, CancellationToken.None);
        (await awaiting.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "the subscription broke"
        );
    }

    [Fact]
    public async Task Disposal_swallows_the_monitor_going_away()
    {
        var gate = new TaskCompletionSource();
        using var monitor = CreateGatedMonitor(
            [],
            gate.Task,
            [],
            new OperationCanceledException("UsbHotplugMonitor was disposed")
        );
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();
        var disposing = notifier.DisposeAsync().AsTask();
        gate.SetResult();

        // Teardown from underneath reaches us as an untokened cancellation. The subscription is
        // over either way, which is exactly what disposal asked for.
        var awaiting = async () => await disposing.WaitAsync(Timeout, CancellationToken.None);
        await awaiting.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Start_throws_when_the_monitor_rejects_the_subscription()
    {
        using var monitor = CreateGatedMonitor(
            [],
            Task.CompletedTask,
            [],
            new NotSupportedException("no hotplug here")
        );
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        // Subscribe reports an unusable monitor on the first read, which happens inside Start's
        // synchronous prologue — so Start reports it instead of deferring it to disposal.
        var start = () => notifier.Start();
        start.Should().Throw<NotSupportedException>().WithMessage("no hotplug here");
    }

    [Fact]
    public async Task Disposing_twice_is_harmless()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        notifier.Start();

        await notifier.DisposeAsync();

        var again = async () => await notifier.DisposeAsync();
        await again.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Disposing_without_starting_is_harmless()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        var disposing = async () => await notifier.DisposeAsync();

        await disposing.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Disposal_leaves_the_monitor_alone()
    {
        using var monitor = CreateFakeMonitor(Connected("device-a"));
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        notifier.Start();

        await notifier.DisposeAsync();

        // The monitor is injected and outlives the notifier: it is a DI singleton.
        A.CallTo(() => monitor.Dispose()).MustNotHaveHappened();
    }

    [Fact]
    public async Task Disposing_asynchronously_from_inside_a_handler_completes()
    {
        var gate = new TaskCompletionSource();
        using var monitor = CreateGatedMonitor(
            [],
            gate.Task,
            [Connected("first"), Connected("second")]
        );
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        var disposing = new TaskCompletionSource<Task>();
        notifier.DeviceConnected += (_, e) =>
        {
            seen.Add(e.Descriptor.DeviceKey);
            // A sync handler cannot await, so hand the disposal task out to the test — it must
            // complete even though the handler that started it is still on the loop's stack.
            disposing.SetResult(notifier.DisposeAsync().AsTask());
        };

        notifier.Start();
        gate.SetResult();

        var disposal = await disposing.Task.WaitAsync(Timeout, CancellationToken.None);
        await disposal.WaitAsync(Timeout, CancellationToken.None);
        seen.Should().Equal("first");
    }

    [Fact]
    public async Task Blocking_on_disposal_from_inside_a_handler_does_not_deadlock()
    {
        var gate = new TaskCompletionSource();
        using var monitor = CreateGatedMonitor(
            [],
            gate.Task,
            [Connected("first"), Connected("second")]
        );
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        var handlerReturned = new TaskCompletionSource();
        notifier.DeviceConnected += (_, e) =>
        {
            seen.Add(e.Descriptor.DeviceKey);
            // Unlike the async variant above, a synchronous wait closes the cycle a disposal must
            // break to stay hang-free: disposal waits for the loop to unwind, and the loop waits
            // for this very handler to return. This is what Dispose-on-shutdown callers do.
            // VSTHRD002: the blocking wait is the subject under test.
#pragma warning disable VSTHRD002
            notifier.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            handlerReturned.SetResult();
        };

        notifier.Start();
        gate.SetResult();

        // Bounded: a disposal that deadlocks never lets the handler return, and a TimeoutException
        // is a far better failure than a hung test run.
        await handlerReturned.Task.WaitAsync(Timeout, CancellationToken.None);
        seen.Should().Equal("first");
    }

    private static IUsbHotplugMonitor CreateFakeMonitor(params UsbHotplugEvent[] events)
    {
        var monitor = A.Fake<IUsbHotplugMonitor>();
        A.CallTo(() => monitor.Subscribe(A<IUsbDeviceFilter?>._, A<CancellationToken>._))
            .ReturnsLazily(
                (IUsbDeviceFilter? _, CancellationToken token) => StreamOf(events, token)
            );
        return monitor;
    }

    private static IUsbHotplugMonitor CreateNeverEndingMonitor()
    {
        var monitor = A.Fake<IUsbHotplugMonitor>();
        A.CallTo(() => monitor.Subscribe(A<IUsbDeviceFilter?>._, A<CancellationToken>._))
            .ReturnsLazily((IUsbDeviceFilter? _, CancellationToken token) => NeverEnding(token));
        return monitor;
    }

    private static IUsbHotplugMonitor CreateGatedMonitor(
        UsbHotplugEvent[] before,
        Task gate,
        UsbHotplugEvent[] after,
        Exception? endWith = null
    )
    {
        var monitor = A.Fake<IUsbHotplugMonitor>();
        A.CallTo(() => monitor.Subscribe(A<IUsbDeviceFilter?>._, A<CancellationToken>._))
            .Returns(GatedStream(before, gate, after, endWith));
        return monitor;
    }

    /// <summary>
    /// Yields <paramref name="before"/>, parks on <paramref name="gate"/>, then yields
    /// <paramref name="after"/> and finally throws <paramref name="endWith"/> if given. Ignoring the
    /// cancellation token is deliberate: it is what lets a test drive the notifier to an exact point
    /// mid-stream and prove the behaviour comes from disposal rather than from cancellation.
    /// </summary>
    private static async IAsyncEnumerable<UsbHotplugEvent> GatedStream(
        UsbHotplugEvent[] before,
        Task gate,
        UsbHotplugEvent[] after,
        Exception? endWith
    )
    {
        foreach (var e in before)
        {
            yield return e;
        }
        // VSTHRD003: the task is foreign on purpose — the test owns it and decides when the stream
        // moves on, which is the only way to hold the notifier at an exact point mid-stream.
#pragma warning disable VSTHRD003
        await gate.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        foreach (var e in after)
        {
            yield return e;
        }
        if (endWith is not null)
        {
            throw endWith;
        }
    }

    /// <summary>
    /// A finite stream. A real subscription never ends on its own, but ending after the burst lets
    /// the mapping tests await <c>RunAsync</c> without cancellation ceremony.
    /// </summary>
    private static async IAsyncEnumerable<UsbHotplugEvent> StreamOf(
        UsbHotplugEvent[] events,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        // Completes synchronously, so the burst below is still delivered inside RunAsync's
        // synchronous window; present only because an async iterator needs an await.
        await Task.CompletedTask;
        foreach (var e in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return e;
        }
    }

    /// <summary>A stream that only ever ends by cancellation, like a real live subscription.</summary>
    private static async IAsyncEnumerable<UsbHotplugEvent> NeverEnding(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        yield break;
    }

    private static UsbHotplugEvent Connected(string key) =>
        new(UsbHotplugEventType.Connected, Device(key));

    private static UsbHotplugEvent Disconnected(string key) =>
        new(UsbHotplugEventType.Disconnected, Device(key));

    private static UsbDeviceDescriptor Device(string key) =>
        new()
        {
            DeviceKey = key,
            BcdUsb = 0x0200,
            VendorId = 0x1234,
        };
}
