using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal.Transfer;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet;

/// <inheritdoc/>
public sealed class UsbInterface : IUsbInterface
{
    // These buffers should be a multiple of the USB endpoint MaxPacketSize.
    // Typical MaxPacketSize values for USB 2.0 and 3.0 are 512 and 1024.
    private const int ReadBufferSize = 32 * 1024;
    private const int WriteBufferSize = 32 * 1024;

    private readonly ILogger<UsbInterface> _logger;
    private readonly UsbDevice _device;
    private readonly IUsbInterfaceDescriptor _descriptor;
    private readonly ISafeDeviceInterface _claimedInterface;
    private readonly byte[] _bulkReadBuffer;
    private readonly GCHandle _bulkReadBufferHandle;
    private readonly Lazy<IUsbEndpointDescriptor> _readEndpoint;
    private readonly object _bulkReadLock = new();
    private readonly byte[] _bulkWriteBuffer;
    private readonly GCHandle _bulkWriteBufferHandle;
    private readonly Lazy<IUsbEndpointDescriptor> _writeEndpoint;
    private readonly object _bulkWriteLock = new();

    /// <summary>
    /// Guards entry into and teardown of _disposeLock. This is a separate lock from _disposeLock
    /// because the latter is a ReaderWriterLockSlim, which throws SynchronizationLockException when
    /// disposed while held or with waiters, so readers must only enter via zero-timeout
    /// TryEnterReadLock while holding this monitor.
    /// </summary>
    private readonly object _disposeLockSync = new();
    private readonly ReaderWriterLockSlim _disposeLock = new();
    private readonly CancellationTokenSource _disposeCts;
    private volatile bool _disposed;

    /// <inheritdoc/>
    public byte Number => _descriptor.InterfaceNumber;

    /// <summary>
    /// A type representing a claimed USB interface.
    /// </summary>
    /// <param name="logger">Logger for this UsbInterface.</param>
    /// <param name="device">The parent USB device.</param>
    /// <param name="descriptor">The USB interface descriptor.</param>
    /// <param name="claimedInterface">A claimed USB device interface.</param>
    /// <param name="readEndpoint">
    /// Optional read endpoint. When nothing is specified and a read operation is attempted,
    /// an attempt is made to pick the first available "input" endpoint for this interface.
    /// </param>
    /// <param name="writeEndpoint">
    /// Optional write endpoint. When nothing is specified and a write operation is attempted,
    /// an attempt is made to pick the first available "output" endpoint for this interface.
    /// </param>
    public UsbInterface(
        ILogger<UsbInterface> logger,
        UsbDevice device,
        IUsbInterfaceDescriptor descriptor,
        ISafeDeviceInterface claimedInterface,
        IUsbEndpointDescriptor? readEndpoint = default,
        IUsbEndpointDescriptor? writeEndpoint = default
    )
    {
        _logger = logger;
        _device = device;
        _descriptor = descriptor;
        _claimedInterface = claimedInterface;
        _bulkReadBuffer = new byte[ReadBufferSize];
        _bulkReadBufferHandle = GCHandle.Alloc(_bulkReadBuffer, GCHandleType.Pinned);
        _readEndpoint = readEndpoint is null
            ? new Lazy<IUsbEndpointDescriptor>(() =>
                GetEndpoint(descriptor, UsbEndpointDirection.Input)
            )
            : new Lazy<IUsbEndpointDescriptor>(readEndpoint);
        _bulkWriteBuffer = new byte[WriteBufferSize];
        _bulkWriteBufferHandle = GCHandle.Alloc(_bulkWriteBuffer, GCHandleType.Pinned);
        _writeEndpoint = writeEndpoint is null
            ? new Lazy<IUsbEndpointDescriptor>(() =>
                GetEndpoint(descriptor, UsbEndpointDirection.Output)
            )
            : new Lazy<IUsbEndpointDescriptor>(writeEndpoint);
        _disposeCts = new CancellationTokenSource();
    }

    /// <inheritdoc/>
    public bool TryGetInputEndpoint([NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint)
    {
        try
        {
            endpoint = _readEndpoint.Value;
            return true;
        }
        catch (InvalidOperationException)
        {
            endpoint = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetOutputEndpoint([NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint)
    {
        try
        {
            endpoint = _writeEndpoint.Value;
            return true;
        }
        catch (InvalidOperationException)
        {
            endpoint = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public UsbResult BulkRead(Span<byte> destination, out int bytesRead, int timeout)
    {
        CheckTransferTimeout(timeout);
        try
        {
            lock (_disposeLockSync)
            {
                // Uses a read lock for both reads and writes, to support duplex.
                //
                // Try enter the lock with a zero timeout. The write lock is only taken by Dispose,
                // so failing to enter means dispose has started. Never block on _disposeLock while
                // holding _disposeLockSync, since Dispose() needs it to exit the write lock.
                if (_disposed || !_disposeLock.TryEnterReadLock(0))
                {
                    throw new ObjectDisposedException(nameof(UsbInterface));
                }
            }
            try
            {
                var bufferLength = Math.Min(destination.Length, ReadBufferSize);
                lock (_bulkReadLock)
                {
                    var result = LibUsbTransfer.ExecuteSync(
                        _logger,
                        _device.Handle,
                        libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK,
                        _readEndpoint.Value.EndpointAddress.RawValue,
                        _bulkReadBufferHandle,
                        bufferLength,
                        timeout > 0 ? (uint)timeout : 0,
                        out bytesRead,
                        _disposeCts.Token
                    );
                    if (bytesRead > 0)
                    {
                        _bulkReadBuffer.AsSpan(0, bytesRead).CopyTo(destination);
                    }
                    return result.ToUsbResult();
                }
            }
            finally
            {
                _disposeLock.ExitReadLock();
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(
                "BulkRead interrupted. {ErrorType}: {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
            bytesRead = 0;
            return UsbResult.Interrupted;
        }
    }

    /// <inheritdoc/>
    public UsbResult BulkWrite(ReadOnlySpan<byte> source, out int bytesWritten, int timeout)
    {
        CheckTransferTimeout(timeout);
        try
        {
            lock (_disposeLockSync)
            {
                // Uses a read lock for both reads and writes, to support duplex.
                //
                // Try enter the lock with a zero timeout. The write lock is only taken by Dispose,
                // so failing to enter means dispose has started. Never block on _disposeLock while
                // holding _disposeLockSync, since Dispose() needs it to exit the write lock.
                if (_disposed || !_disposeLock.TryEnterReadLock(0))
                {
                    throw new ObjectDisposedException(nameof(UsbInterface));
                }
            }
            try
            {
                var bufferLength = Math.Min(source.Length, WriteBufferSize);
                lock (_bulkWriteLock)
                {
                    source[..bufferLength].CopyTo(_bulkWriteBuffer.AsSpan(0, bufferLength));
                    return LibUsbTransfer
                        .ExecuteSync(
                            _logger,
                            _device.Handle,
                            libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK,
                            _writeEndpoint.Value.EndpointAddress.RawValue,
                            _bulkWriteBufferHandle,
                            bufferLength,
                            timeout > 0 ? (uint)timeout : 0,
                            out bytesWritten,
                            _disposeCts.Token
                        )
                        .ToUsbResult();
                }
            }
            finally
            {
                _disposeLock.ExitReadLock();
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(
                "BulkWrite interrupted. {ErrorType}: {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
            bytesWritten = 0;
            return UsbResult.Interrupted;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"#{_descriptor.InterfaceNumber} "
        + $"({_descriptor.InterfaceClass} subclass {_descriptor.InterfaceSubClass})";

    private IUsbEndpointDescriptor GetEndpoint(
        IUsbInterfaceDescriptor descriptor,
        UsbEndpointDirection direction
    )
    {
        var endpoint = descriptor.GetEndpoint(direction, out var count);
        if (count > 1)
        {
            _logger.LogWarning(
                "Interface #{InterfaceNumber} has {EndpointCount} {EndpointDirection} endpoints. "
                    + "The first endpoint was selected.",
                descriptor.InterfaceNumber,
                count,
                direction
            );
        }
        return endpoint;
    }

    /// <summary>
    /// Throw ArgumentOutOfRangeException when timeout is 0 or less than -1.
    /// </summary>
    private static void CheckTransferTimeout(int timeout)
    {
        if (timeout is 0 or < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Invalid timeout; must be greater than 0 or -1 (infinite)."
            );
        }
    }

    /// <summary>
    /// Disposes this interface and associated resources. Ongoing read and write
    /// transfers are canceled and allocated read and write memory buffers are freed.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeCts)
        {
            if (_disposed)
            {
                _logger.LogDebug("Interface {UsbInterface} already disposed.", this);
                return;
            }
            // Prevent new transfers from starting and cancel any ongoing
            _disposeCts.Cancel();
            _disposeLock.EnterWriteLock();
            try
            {
                // Ask UsbDevice to remove it from list of open interfaces
                _device.ReleaseInterface(_descriptor.InterfaceNumber);
                _claimedInterface.Dispose();
                // Free read and write buffers
                _bulkReadBufferHandle.Free();
                _bulkWriteBufferHandle.Free();
                _disposeCts.Dispose();
                _disposed = true;
            }
            finally
            {
                // Safe to dispose _disposeLock here: readers only TryEnter it with a zero-timeout
                // while holding _disposeLockSync, and the only writer is this thread via Dispose.
                lock (_disposeLockSync)
                {
                    _disposeLock.ExitWriteLock();
                    _disposeLock.Dispose();
                }
            }
        }
    }
}
