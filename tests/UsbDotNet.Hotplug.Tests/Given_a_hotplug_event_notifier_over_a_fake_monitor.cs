using System.Collections.Concurrent;
using System.Threading.Channels;
using FakeItEasy;
using UsbDotNet.Descriptor;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_hotplug_event_notifier_over_a_fake_monitor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Dispose_from_within_a_handler_does_not_deadlock()
    {
        var channel = Channel.CreateUnbounded<UsbHotplugEvent>();
        using var monitor = CreateFakeMonitor(channel);
        // Also disposed from within the handler below; Dispose is idempotent so the scope-exit
        // Dispose here (which satisfies CA2000) is harmless.
        using var notifier = new UsbHotplugEventNotifier(monitor);

        using var disposeReturned = new ManualResetEventSlim(false);
        notifier.DeviceConnected += (_, _) =>
        {
            // Disposing from inside a handler runs on the pump thread; a naive synchronous wait on
            // the pump would deadlock the thread against itself. This must return promptly instead.
            notifier.Dispose();
            disposeReturned.Set();
        };
        notifier.Start();

        channel
            .Writer.TryWrite(
                new UsbHotplugEvent(
                    UsbHotplugEventType.Connected,
                    new UsbDeviceDescriptor { DeviceKey = "fake-device" }
                )
            )
            .Should()
            .BeTrue();

        disposeReturned
            .Wait(Timeout)
            .Should()
            .BeTrue(because: "Dispose called from within a handler must not deadlock");
    }

    [Fact]
    public void Start_after_Dispose_throws_ObjectDisposedException()
    {
        var channel = Channel.CreateUnbounded<UsbHotplugEvent>();
        using var monitor = CreateFakeMonitor(channel);
        using var notifier = new UsbHotplugEventNotifier(monitor);

        notifier.Dispose();

        FluentActions.Invoking(notifier.Start).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Concurrent_Start_and_Dispose_never_fault_the_pump_task()
    {
        // Regression for a Start/Dispose race: Dispose could observe _pump == null and dispose
        // the CTS while Start was between its disposed check and the pump creation, so the pump
        // lambda later read Token from a disposed CTS and faulted unobserved on a thread-pool
        // thread. The interleaving cannot be forced deterministically from the outside, so race
        // the two calls repeatedly and fail if any abandoned task faulted.
        var unobserved = new ConcurrentQueue<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> onUnobserved = (_, e) =>
        {
            unobserved.Enqueue(e.Exception);
            e.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += onUnobserved;
        try
        {
            for (var i = 0; i < 500; i++)
            {
                var channel = Channel.CreateUnbounded<UsbHotplugEvent>();
                using var monitor = CreateFakeMonitor(channel);
                using var notifier = new UsbHotplugEventNotifier(monitor);
                var start = Task.Run(() =>
                {
                    try
                    {
                        notifier.Start();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Valid outcome: Dispose won the race.
                    }
                });
                var dispose = Task.Run(notifier.Dispose);
                await Task.WhenAll(start, dispose);
            }
            // Faulted-and-abandoned tasks only surface through finalization.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            unobserved.Should().BeEmpty();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= onUnobserved;
        }
    }

    private static IUsbHotplugMonitor CreateFakeMonitor(Channel<UsbHotplugEvent> channel)
    {
        var monitor = A.Fake<IUsbHotplugMonitor>();
        A.CallTo(() => monitor.Subscribe(A<UsbDeviceFilter?>._))
            .ReturnsLazily(() => CreateFakeSubscription(channel));
        A.CallTo(() => monitor.Dispose()).Invokes(() => channel.Writer.TryComplete());
        return monitor;
    }

    private static IUsbHotplugSubscription CreateFakeSubscription(Channel<UsbHotplugEvent> channel)
    {
        var subscription = A.Fake<IUsbHotplugSubscription>();
        A.CallTo(() => subscription.Reader).Returns(channel.Reader);
        A.CallTo(() => subscription.Dispose()).Invokes(() => channel.Writer.TryComplete());
        return subscription;
    }
}
