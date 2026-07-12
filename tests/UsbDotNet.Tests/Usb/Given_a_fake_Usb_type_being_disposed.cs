using FakeItEasy;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.Tests.Fakes;

namespace UsbDotNet.Tests.Usb;

/// <summary>
/// Usb.Dispose releases _lock while joining the event-loop thread (holding it would deadlock
/// with hotplug dispatch). These tests park the fake event loop inside HandleEventsCompleted to
/// hold Dispose in that window and verify that no API call can slip in and that a concurrent
/// Dispose does not return before teardown has completed.
/// </summary>
public sealed class Given_a_fake_Usb_type_being_disposed : IDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Set when the event-loop thread is parked inside HandleEventsCompleted.</summary>
    private readonly ManualResetEventSlim _eventLoopEntered = new(false);

    /// <summary>Set by the test to let the parked event-loop thread exit.</summary>
    private readonly ManualResetEventSlim _eventLoopRelease = new(false);

    private readonly ISafeContext _context;
    private readonly UsbDotNet.Usb _usb;

    public Given_a_fake_Usb_type_being_disposed(ITestOutputHelper output)
    {
        _loggerFactory = new TestLoggerFactory(output);
        _context = FakeHelper.CreateBlockingFakeContext(_eventLoopEntered, _eventLoopRelease);
        _usb = new UsbDotNet.Usb(
            FakeHelper.CreateFakeLibUsb(_context, hasCapability: true),
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.None }
        );
    }

    [Fact]
    public async Task Api_calls_during_teardown_throw_ObjectDisposedException()
    {
        var dispose = BeginBlockedDispose();

        FluentActions
            .Invoking(() => _usb.GetDeviceList())
            .Should()
            .Throw<ObjectDisposedException>();
        FluentActions
            .Invoking(() => _usb.OpenDevice("1234:5678:3:17"))
            .Should()
            .Throw<ObjectDisposedException>();
        // A hotplug registration in this window would leak: its callback handle is disposed by
        // nobody and pins the native context past _context.Dispose().
        FluentActions
            .Invoking(() => ((IHotplugProvider)_usb).RegisterHotplug())
            .Should()
            .Throw<ObjectDisposedException>();
        dispose.IsCompleted.Should().BeFalse("teardown is still joining the event-loop thread");

        _eventLoopRelease.Set();
        await dispose.WaitAsync(WaitTimeout);
        _context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task A_concurrent_Dispose_call_returns_only_after_teardown_completes()
    {
        var first = BeginBlockedDispose();

        var second = Task.Run(_usb.Dispose);
        var completedEarly = await Task.WhenAny(second, Task.Delay(250));
        completedEarly
            .Should()
            .NotBe(second, "Dispose must not return while teardown is still in progress");

        _eventLoopRelease.Set();
        await Task.WhenAll(first, second).WaitAsync(WaitTimeout);
        _context.IsClosed.Should().BeTrue();

        // Dispose returning implies teardown finished, so the single-instance slot is free
        // again and a replacement Usb can be constructed right away.
        using var replacement = new UsbDotNet.Usb(A.Fake<ILibUsb>());
    }

    /// <summary>
    /// Starts Dispose on a background thread and returns once it is parked in the window where
    /// _lock is released to join the (gated) event-loop thread.
    /// </summary>
    private Task BeginBlockedDispose()
    {
        _usb.Initialize();
        _eventLoopEntered.Wait(WaitTimeout).Should().BeTrue();

        var dispose = Task.Run(_usb.Dispose);
        SpinWait
            .SpinUntil(
                () =>
                {
                    try
                    {
                        _ = _usb.GetDeviceList();
                        return false;
                    }
                    catch (ObjectDisposedException)
                    {
                        return true;
                    }
                },
                WaitTimeout
            )
            .Should()
            .BeTrue("the disposer should reach the event-loop join window");
        return dispose;
    }

    public void Dispose()
    {
        _eventLoopRelease.Set(); // Unblock the event loop if a failed test left it parked
        _usb.Dispose();
        _context.Dispose(); // Usually already disposed by Usb.Dispose; the fake is idempotent
        _loggerFactory.Dispose();
        _eventLoopEntered.Dispose();
        _eventLoopRelease.Dispose();
    }
}
