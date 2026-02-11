//
// RundownGuard
//
// Purpose
// - Coordinates concurrent (shared) work and critical (exclusive) operations.
// - Provides a cooperative "rundown" phase that stops accepting new work and waits
//   for all in-flight holders to drain during Dispose().
// - Uses Stopwatch-based, monotonic timeout accounting for low overhead.
//
// When to use
// - Lifecycle-sensitive components (e.g., device access, I/O pipelines) that must:
//   1) Stop accepting new work,
//   2) Wait for current work to finish,
//   3) Perform teardown safely.
//
// Key concepts
// - Shared: Multiple concurrent holders (bounded by maxSharedCount) when no exclusive holder exists.
// - Exclusive: Single holder; no concurrent shared holders.
// - Rundown: Once started (TriggerRundown or Dispose), new acquisitions are rejected.
//
// Notes
// - AcquireShared/AcquireExclusive throw ObjectDisposedException once shutdown has started,
//   and TimeoutException if an explicit timeout elapses while waiting.
// - TriggerRundown() prevents new acquisitions and wakes waiters (does not block).
// - Dispose() prevents new acquisitions and blocks until all holders have released, then returns.
//   Calling Dispose() again throws ObjectDisposedException.
// - Always dispose returned tokens to release the corresponding hold.
//

using System.Diagnostics;

namespace UsbDotNet.Internal;

/// <summary>
/// Coordinates shared and exclusive access and provides a cooperative rundown (shutdown) mechanism.
/// Uses Stopwatch-based timeout tracking for monotonic, low-overhead waits.
/// </summary>
/// <remarks>
/// Typical usage:
/// - Normal operation: acquire shared tokens for read-like work; acquire exclusive tokens for state mutations.
/// - Shutdown:
///   - Call <see cref="TriggerRundown"/> to immediately reject new acquisitions without blocking; or
///   - Call <see cref="Dispose"/> to reject new acquisitions and block until existing holders drain.
/// Fairness: when there are exclusive waiters, new shared acquisitions are held back to avoid starving exclusives.
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// var guard = new RundownGuard();
///
/// // Shared usage
/// using (guard.AcquireSharedToken(TimeSpan.FromSeconds(1)))
/// {
///     // Do concurrent work
/// }
///
/// // Exclusive usage
/// using (guard.AcquireExclusiveToken(TimeSpan.FromSeconds(5)))
/// {
///     // Perform critical operation that must not overlap with shared work
/// }
///
/// // Initiate shutdown (option A): prevent new acquisitions; existing holders continue until release.
/// guard.TriggerRundown();
///
/// // Initiate shutdown (option B): block until all holders are released, then return.
/// guard.Dispose();
/// ]]></code>
/// </example>
internal class RundownGuard : IDisposable
{
    /// <summary>
    /// Maximum number of concurrent shared holders allowed.
    /// </summary>
    private readonly int _maxSharedCount;

    /// <summary>
    /// Number of currently active shared holders.
    /// </summary>
    private int _activeCount;

    /// <summary>
    /// Indicates shutdown intent; once true, no new acquisitions are allowed.
    /// </summary>
    private bool _isShuttingDown;

    /// <summary>
    /// True while an exclusive token is held.
    /// </summary>
    private bool _exclusiveHeld;

    /// <summary>
    /// Number of threads currently waiting for exclusive access.
    /// </summary>
    private int _exclusiveWaiters;

    /// <summary>
    /// Rundown state flag: set once rundown begins (Dispose or TriggerRundown + drain).
    /// </summary>
    private bool _rundownStarted;

    /// <summary>
    /// Intrinsic lock guarding state and used as the monitor for wait/pulse.
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new <see cref="RundownGuard"/>.
    /// </summary>
    /// <param name="maxSharedCount">
    /// Maximum number of concurrent shared holders allowed; use <see cref="int.MaxValue"/> for unlimited.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="maxSharedCount"/> is not positive.</exception>
    public RundownGuard(int maxSharedCount = int.MaxValue)
    {
        if (maxSharedCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSharedCount),
                "Must be positive or int.MaxValue for unlimited."
            );
        }

        _maxSharedCount = maxSharedCount;
    }

    /// <summary>
    /// Acquires a shared token.
    /// </summary>
    /// <param name="timeout">Optional timeout. If expired, a <see cref="TimeoutException"/> is thrown.</param>
    /// <returns>An <see cref="IDisposable"/> token that must be disposed to release the shared hold. Never null.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if shutdown has started.</exception>
    /// <exception cref="TimeoutException">Thrown if the wait exceeds <paramref name="timeout"/>.</exception>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// using var token = guard.AcquireSharedToken(TimeSpan.FromSeconds(1));
    /// // Do work under shared protection
    /// ]]></code>
    /// </example>
    public IDisposable AcquireSharedToken(TimeSpan? timeout = null)
    {
        AcquireShared(timeout);
        return new ProtectionToken(this);
    }

    /// <summary>
    /// Acquires shared access.
    /// </summary>
    /// <param name="timeout">Optional timeout. If expired, a <see cref="TimeoutException"/> is thrown.</param>
    /// <exception cref="ObjectDisposedException">Thrown if shutdown has started.</exception>
    /// <exception cref="TimeoutException">Thrown if the wait exceeds <paramref name="timeout"/>.</exception>
    public void AcquireShared(TimeSpan? timeout = null)
    {
        // Create a Stopwatch once to compute remaining time on each wait.
        var sw = timeout is null ? null : Stopwatch.StartNew();

        lock (_lock)
        {
            while (true)
            {
                // Reject new shared acquisitions during shutdown.
                if (_isShuttingDown)
                {
                    throw new ObjectDisposedException(
                        nameof(RundownGuard),
                        "Rundown has started, no new shared acquisitions allowed."
                    );
                }

                // Allow shared acquisition only when:
                // - No exclusive holder is present,
                // - No exclusive waiter exists (to avoid starvation),
                // - Shared count has not reached the limit.
                if (!_exclusiveHeld && _exclusiveWaiters == 0 && _activeCount < _maxSharedCount)
                {
                    _activeCount++;
                    return;
                }

                // Otherwise, wait until a state change occurs or timeout elapses.
                AcquireLock(sw, timeout);
            }
        }
    }

    /// <summary>
    /// Acquires an exclusive token.
    /// </summary>
    /// <param name="timeout">Optional timeout. If expired, a <see cref="TimeoutException"/> is thrown.</param>
    /// <returns>An <see cref="IDisposable"/> token that must be disposed to release the exclusive hold. Never null.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if shutdown has started.</exception>
    /// <exception cref="TimeoutException">Thrown if the wait exceeds <paramref name="timeout"/>.</exception>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// using var token = guard.AcquireExclusiveToken(TimeSpan.FromSeconds(5));
    /// // Perform critical operation under exclusive protection
    /// ]]></code>
    /// </example>
    public IDisposable AcquireExclusiveToken(TimeSpan? timeout = null)
    {
        AcquireExclusive(timeout);
        return new ExclusiveToken(this);
    }

    /// <summary>
    /// Acquires exclusive access.
    /// </summary>
    /// <param name="timeout">Optional timeout. If expired, a <see cref="TimeoutException"/> is thrown.</param>
    /// <exception cref="ObjectDisposedException">Thrown if shutdown has started.</exception>
    /// <exception cref="TimeoutException">Thrown if the wait exceeds <paramref name="timeout"/>.</exception>
    public void AcquireExclusive(TimeSpan? timeout = null)
    {
        // Create a Stopwatch once to compute remaining time on each wait.
        var sw = timeout is null ? null : Stopwatch.StartNew();

        lock (_lock)
        {
            // Register as an exclusive waiter to avoid starvation from shared acquires.
            _exclusiveWaiters++;

            try
            {
                while (true)
                {
                    // Reject new exclusive acquisitions during shutdown.
                    if (_isShuttingDown)
                    {
                        throw new ObjectDisposedException(
                            nameof(RundownGuard),
                            "Rundown has started, no new exclusive acquisitions allowed."
                        );
                    }

                    // Acquire exclusivity only when no shared holders are active and no exclusive holder exists.
                    if (!_exclusiveHeld && _activeCount == 0)
                    {
                        _exclusiveHeld = true;
                        return;
                    }

                    // Otherwise, wait for a state change or timeout.
                    AcquireLock(sw, timeout);
                }
            }
            finally
            {
                _exclusiveWaiters--;
            }
        }
    }

    /// <summary>
    /// Wait on the condition variable with an optional Stopwatch-based timeout.
    /// Throws TimeoutException if the timeout elapses.
    /// </summary>
    private void AcquireLock(Stopwatch? stopwatch, TimeSpan? timeout)
    {
        if (timeout is null)
        {
            _ = Monitor.Wait(_lock);
        }
        else
        {
            var remaining = timeout.Value - stopwatch!.Elapsed;
            if (remaining <= TimeSpan.Zero || !Monitor.Wait(_lock, remaining))
                throw new TimeoutException();
        }
    }

    /// <summary>
    /// Signals that shutdown should begin by preventing new acquisitions.
    /// Does not block; existing holders continue until they release.
    /// </summary>
    /// <remarks>
    /// Optional; calling <see cref="Dispose"/> will also initiate shutdown and will block until drain.
    /// </remarks>
    public void TriggerRundown()
    {
        lock (_lock)
        {
            _isShuttingDown = true;
            // Wake any waiters so they can observe the shutdown state.
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// Initiates rundown and waits for in-flight holders to drain.
    /// </summary>
    /// <remarks>
    /// - First call: sets shutdown, waits until all shared and exclusive holders release, then returns.
    /// - Subsequent calls: throw <see cref="ObjectDisposedException"/>.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if rundown has already started.
    /// </exception>
    public void Dispose()
    {
        lock (_lock)
        {
            // If already started, consider the instance disposed and reject further Dispose calls.
            if (_rundownStarted)
            {
                throw new ObjectDisposedException(
                    nameof(RundownGuard),
                    "Dispose/rundown completed, no additional Dispose calls allowed."
                );
            }

            // Become the rundown owner: prevent new acquisitions.
            _rundownStarted = true;
            _isShuttingDown = true;

            // Wait for all shared and exclusive holders to release.
            while (_activeCount > 0 || _exclusiveHeld)
            {
                _ = Monitor.Wait(_lock);
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases a shared acquisition and wakes waiters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no shared guards are currently held.</exception>
    public void ReleaseShared()
    {
        lock (_lock)
        {
            if (_activeCount <= 0)
            {
                throw new InvalidOperationException("No shared guards held.");
            }
            _activeCount--;
            // Wake up threads that might be waiting for capacity or for all shared to drain.
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// Releases an exclusive acquisition and wakes waiters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no exclusive guard is currently held.</exception>
    public void ReleaseExclusive()
    {
        lock (_lock)
        {
            if (_exclusiveHeld == false)
            {
                throw new InvalidOperationException("No exclusive guard held.");
            }

            _exclusiveHeld = false;
            // Wake up threads waiting for exclusivity or rundown progress.
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// Opaque token returned by <see cref="AcquireSharedToken"/>; disposing it releases the shared hold.
    /// </summary>
    private sealed class ProtectionToken : IDisposable
    {
        private readonly RundownGuard _owner;

        internal ProtectionToken(RundownGuard owner)
        {
            _owner = owner;
        }

        public void Dispose() => _owner.ReleaseShared();
    }

    /// <summary>
    /// Opaque token returned by <see cref="AcquireExclusiveToken"/>; disposing it releases the exclusive hold.
    /// </summary>
    private sealed class ExclusiveToken : IDisposable
    {
        private readonly RundownGuard _owner;

        internal ExclusiveToken(RundownGuard owner)
        {
            _owner = owner;
        }

        public void Dispose() => _owner.ReleaseExclusive();
    }
}
