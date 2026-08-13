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
        var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        notifier.DeviceConnected += (_, e) => seen.Add(e.Descriptor.DeviceKey);

        await notifier.RunAsync(CancellationToken.None);

        seen.Should().Equal("device-a", "device-b");
    }

    [Fact]
    public async Task Each_event_type_reaches_only_its_own_handler()
    {
        using var monitor = CreateFakeMonitor(Connected("arrived"), Disconnected("left"));
        var notifier = new UsbHotplugEventNotifier(monitor);
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
        var notifier = new UsbHotplugEventNotifier(monitor);

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
        var notifier = new UsbHotplugEventNotifier(monitor);
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
        var notifier = new UsbHotplugEventNotifier(monitor);

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
        var notifier = new UsbHotplugEventNotifier(monitor);

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
        var notifier = new UsbHotplugEventNotifier(monitor, filter);

        await notifier.RunAsync(cts.Token);

        A.CallTo(() => monitor.Subscribe(filter, cts.Token)).MustHaveHappenedOnceExactly();
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
