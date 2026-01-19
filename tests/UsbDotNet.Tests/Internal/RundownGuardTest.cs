using UsbDotNet.Internal;

namespace UsbDotNet.Tests.Internal;

public class RundownGuardTest
{
    [Fact]
    public void AcquireShared_returns_immediately_when_no_other_guards_are_held()
    {
        using var guard = new RundownGuard();
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().NotThrow();
        guard.ReleaseShared();
    }

    [Fact]
    public void AcquireShared_waits_for_release_when_ExclusiveGuard_is_held()
    {
        using var guard = new RundownGuard();
        var exclusive = guard.AcquireExclusiveToken()!;
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<TimeoutException>().WithMessage("The operation has timed out.");
        exclusive.Dispose();
        act.Should().NotThrow();
        guard.ReleaseShared();
    }

    [Fact]
    public void AcquireShared_throws_TimeoutException_when_wait_timeout_is_reached()
    {
        using var guard = new RundownGuard();
        var exclusive = guard.AcquireExclusiveToken()!;
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<TimeoutException>().WithMessage("The operation has timed out.");
        exclusive.Dispose();
    }

    [Fact]
    public void AcquireShared_returns_immediately_when_maxSharedCount_is_not_reached()
    {
        using var guard = new RundownGuard(2);
        guard.AcquireSharedToken();
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().NotThrow();
        guard.ReleaseShared();
        guard.ReleaseShared();
    }

    [Fact]
    public void AcquireShared_waits_for_release_when_maxSharedCount_is_reached()
    {
        using var guard = new RundownGuard(2);
        var shared = guard.AcquireSharedToken()!;
        _ = guard.AcquireSharedToken();
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<TimeoutException>().WithMessage("The operation has timed out.");
        shared.Dispose();
        act.Should().NotThrow();
        guard.ReleaseShared();
        guard.ReleaseShared();
    }

    [Fact]
    public void AcquireShared_throws_ObjectDisposedException_when_rundown_is_triggered()
    {
        using var guard = new RundownGuard();
        guard.TriggerRundown();
        var act = () => guard.AcquireSharedToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Calling_ReleaseShared_when_no_guard_is_aquired_should_throw()
    {
        using var guard = new RundownGuard();
        var act = () => guard.ReleaseShared();
        act.Should().Throw<InvalidOperationException>().WithMessage("No shared guards held.");
    }

    [Fact]
    public void AcquireExclusive_returns_immediately_when_no_other_guards_are_held()
    {
        using var guard = new RundownGuard();
        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().NotThrow();
        guard.ReleaseExclusive();
    }

    [Fact]
    public void AcquireExclusive_waits_for_all_other_guards_to_release()
    {
        using var guard = new RundownGuard();
        var exclusive = guard.AcquireExclusiveToken()!;
        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<TimeoutException>().WithMessage("The operation has timed out.");
        exclusive.Dispose();
        act.Should().NotThrow();
        guard.ReleaseExclusive();
    }

    [Fact]
    public void AcquireExclusive_throws_TimeoutException_when_wait_timeout_is_reached()
    {
        using var guard = new RundownGuard();
        var exclusive = guard.AcquireExclusiveToken()!;
        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<TimeoutException>().WithMessage("The operation has timed out.");
        exclusive.Dispose();
    }

    [Fact]
    public void AcquireExclusive_throws_ObjectDisposedException_when_rundown_is_triggered()
    {
        using var guard = new RundownGuard();
        guard.TriggerRundown();
        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Calling_ReleaseExclusive_when_no_guard_is_aquired_should_throw()
    {
        using var guard = new RundownGuard();
        var act = () => guard.ReleaseExclusive();
        act.Should().Throw<InvalidOperationException>().WithMessage("No exclusive guard held.");
    }

    [Fact]
    public void Dispose_triggers_rundown()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        // Can't use using pattern here, dispose throws when already disposed.
        var guard = new RundownGuard();
#pragma warning restore CA2000 // Dispose objects before losing scope
        var exclusive = guard.AcquireExclusiveToken()!;
        var worker = new Thread(() => guard.Dispose());
        worker.Start();
        exclusive.Dispose();
        worker.Join();

        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<ObjectDisposedException>();
    }

    /*
    // WaitForRundown was removed from the API; keeping this example commented out for reference.
    [Fact]
    public void WaitForRundown_waits_for_all_guards_to_release()
    {
        var guard = new RundownGuard();
        var exclusive = guard.AcquireExclusiveToken()!;
        var worker1 = new Thread(
            () =>
            {
                guard.AcquireSharedToken();
                guard.ReleaseShared();
            }
        );
        worker1.Start();
        worker1.Join();

        // No longer applicable:
        // var worker2 = new Thread(() => guard.WaitForRundown());
        // worker2.Start();
        // worker2.Join();

        exclusive.Dispose();
        // After rundown, acquisitions throw ObjectDisposedException rather than returning null.
        var act = () => guard.AcquireExclusiveToken(TimeSpan.FromMilliseconds(1));
        act.Should().Throw<ObjectDisposedException>();

        worker1.Join();
    }
    */
}
