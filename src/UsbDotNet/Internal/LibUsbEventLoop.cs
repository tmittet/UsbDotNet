using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.Internal;

/// <summary>
/// libusb does not start any threads of its own. Async operations are driven by this event loop.
/// See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#asyncevent for info.
/// </summary>
internal sealed class LibUsbEventLoop : IDisposable
{
    private readonly object _lock = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LibUsbEventLoop> _logger;
    private readonly ISafeContext _context;
    private readonly CancellationTokenSource _cts;
    private readonly IntPtr _completedPtr;
    private Thread? _thread;
    private bool _disposed;

    public LibUsbEventLoop(ILoggerFactory loggerFactory, ISafeContext context)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<LibUsbEventLoop>();
        _context = context;
        _cts = new CancellationTokenSource();
        _completedPtr = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(_completedPtr, 0);
    }

    /// <summary>
    /// Start the background thread that handles libusb events. All libusb event handling is
    /// performed on this thread. libusb does not invoke any callbacks outside of this context.
    /// Consequently, all registered callbacks will be run on this thread.
    /// See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            CheckDisposed();
            if (_thread is not null)
            {
                throw new InvalidOperationException("LibUsbEventLoop already started.");
            }
            _thread = new Thread(() => HandleEventsLoop(_cts.Token)) { IsBackground = true };
            _thread.Start();
        }
    }

    private void HandleEventsLoop(CancellationToken token)
    {
        try
        {
            _logger.LogDebug("HandleEventsLoop started.");
            while (!token.IsCancellationRequested)
            {
                // libusb does not write to completed, so there is no reason to check it
                // See: https://github.com/libusb/libusb/blob/master/libusb/io.c
                var result = _context.HandleEventsCompleted(_completedPtr);
                // libusb_handle_events can return LIBUSB_ERROR_INTERRUPTED transiently;
                // do not exit the loop on LIBUSB_ERROR_INTERRUPTED.
                if (result is not 0 and not libusb_error.LIBUSB_ERROR_INTERRUPTED)
                {
                    _logger.LogWarning(
                        "LibUsb HandleEvents failed; exiting event loop: {ErrorMessage}.",
                        result.GetMessage()
                    );
                    break;
                }
#if DEBUG
                var completed = Marshal.ReadInt32(_completedPtr) != 0;
                _logger.LogTrace("libusb_handle_events_completed '{Completed}'.", completed);
#endif
            }
            _logger.LogDebug("HandleEventsLoop stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleEventsLoop failed.");
        }
    }

    /// <summary>
    /// Throw ObjectDisposedException when LibUsbEventLoop is disposed.
    /// </summary>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LibUsbEventLoop));
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_thread is not null)
            {
                // Signal cancellation here to stop the HandleEventsLoop
                _cts.Cancel();
                // Set completed = 1 so libusb_handle_events_completed exits if currently blocking
                Marshal.WriteInt32(_completedPtr, 1);
                // Call libusb_interrupt_event_handler to unblock event handler and allow exit.
                // This is required since we don't follow the exact pattern recommended by libusb.
                // See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html#eventthread
                // and: https://libusb.sourceforge.io/api-1.0/group__libusb__poll.html#ga188b6c50944b49f122ccfd45b93fa9f2
                // We deregister hotplug events first, which wakes up libusb_handle_events, then
                // stop the event loop; hence the event handler would block forever. Calling
                // libusb_interrupt_event_handler ensures it wakes up.
                _context.InterruptEventHandler();
                // Wait for libusb_handle_events_completed and the HandleEventsLoop to stop
                _thread.Join();
            }
            Marshal.FreeHGlobal(_completedPtr);
            _cts.Dispose();
        }
    }
}
