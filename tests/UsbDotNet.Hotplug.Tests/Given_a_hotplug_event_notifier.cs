using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Hotplug.Tests;

/// <summary>
/// The notifier over a real <see cref="UsbHotplugMonitor"/>, with the provider faked so arrivals can
/// be driven directly. Deterministic, and no device required: because nothing in the notifier or the
/// monitor starts a task of its own, the subscription is registered by the time
/// <c>RunAsync</c> returns its Task.
/// </summary>
public sealed class Given_a_hotplug_event_notifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task An_arrival_raises_DeviceConnected()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var arrived = new TaskCompletionSource<IUsbDeviceDescriptor>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        notifier.DeviceConnected += (_, e) => arrived.TrySetResult(e.Descriptor);

        // RunAsync runs the iterator body synchronously up to the point where it parks on the empty
        // channel, so the subscription is live as soon as the call returns — no polling needed.
        var run = notifier.RunAsync(cts.Token);
        monitor
            .SubscriptionCount.Should()
            .Be(1, because: "RunAsync registers before it can park on an empty stream");

        RaiseArrived(monitor, Device("real-device"));

        var descriptor = await arrived.Task.WaitAsync(Timeout, CancellationToken.None);
        descriptor.DeviceKey.Should().Be("real-device");

        await CancelAndAwait(cts, run);
        monitor
            .SubscriptionCount.Should()
            .Be(0, because: "unwinding RunAsync disposes the enumerator, which unsubscribes");
    }

    [Fact]
    public async Task A_removal_raises_DeviceDisconnected()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var left = new TaskCompletionSource<IUsbDeviceDescriptor>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        notifier.DeviceDisconnected += (_, e) => left.TrySetResult(e.Descriptor);

        var run = notifier.RunAsync(cts.Token);
        RaiseArrived(monitor, Device("real-device"));
        RaiseLeft(monitor, Device("real-device"));

        var descriptor = await left.Task.WaitAsync(Timeout, CancellationToken.None);
        descriptor.DeviceKey.Should().Be("real-device");

        await CancelAndAwait(cts, run);
    }

    [Fact]
    public async Task Monitor_dispose_surfaces_a_cancellation_from_RunAsync()
    {
        var provider = CreateFakeProvider();
        var monitor = new UsbHotplugMonitor(provider);
        using var cts = new CancellationTokenSource(Timeout);
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        var run = notifier.RunAsync(cts.Token);
        monitor.SubscriptionCount.Should().Be(1);

        monitor.Dispose();

        // Teardown reaches an events consumer exactly as it reaches a stream consumer: an
        // OperationCanceledException carrying no token, which is what tells it apart from the
        // consumer's own cancellation.
        var awaiting = async () => await run.WaitAsync(Timeout, CancellationToken.None);
        (
            await awaiting
                .Should()
                .ThrowAsync<OperationCanceledException>()
                .WithMessage("*UsbHotplugMonitor was disposed*")
        )
            .Which.CancellationToken.Should()
            .Be(CancellationToken.None);
    }

    [Fact]
    public async Task Start_registers_the_subscription_before_it_returns()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        await using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Start();

        monitor
            .SubscriptionCount.Should()
            .Be(1, because: "Start registers before it can park on an empty stream");
    }

    [Fact]
    public async Task Disposal_unsubscribes_from_the_monitor()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        notifier.Start();

        await notifier.DisposeAsync().AsTask().WaitAsync(Timeout, CancellationToken.None);

        // Awaiting the run loop is what makes this observable at the point disposal returns: the
        // enumerator's finally is what unsubscribes.
        monitor.SubscriptionCount.Should().Be(0);
    }

    [Fact]
    public async Task An_arrival_after_disposal_reaches_no_handler()
    {
        var provider = CreateFakeProvider();
        using var monitor = new UsbHotplugMonitor(provider);
        await using var notifier = new UsbHotplugEventNotifier(monitor);
        var seen = new List<string>();
        notifier.DeviceConnected += (_, e) => seen.Add(e.Descriptor.DeviceKey);
        notifier.Start();

        await notifier.DisposeAsync().AsTask().WaitAsync(Timeout, CancellationToken.None);
        RaiseArrived(monitor, Device("too-late"));

        seen.Should().BeEmpty();
    }

    /// <summary>Ends a live run and observes its cancellation, bounded so a hang fails the test.</summary>
    private static async Task CancelAndAwait(CancellationTokenSource cts, Task run)
    {
        await cts.CancelAsync();
        var awaiting = async () => await run.WaitAsync(Timeout, CancellationToken.None);
        _ = await awaiting.Should().ThrowAsync<OperationCanceledException>();
    }

    private static IHotplugProvider CreateFakeProvider()
    {
        var provider = A.Fake<IHotplugProvider>();
        A.CallTo(() => provider.IsHotplugSupported).Returns(true);
        A.CallTo(() => provider.RegisterHotplug(A<IHotplugListener>._))
            .Returns(HotplugRegistrationResult.Success);
        return provider;
    }

    private static UsbDeviceDescriptor Device(string key) =>
        new()
        {
            DeviceKey = key,
            BcdUsb = 0x0200,
            VendorId = 0x1234,
        };

    private static void RaiseArrived(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceArrived(descriptor);

    private static void RaiseLeft(UsbHotplugMonitor monitor, UsbDeviceDescriptor descriptor) =>
        ((IHotplugListener)monitor).OnDeviceLeft(descriptor);
}
