using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal.Transfer;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet;

/// <inheritdoc/>
public sealed class UsbInterface : IUsbInterface
{
    // The transfer buffer size should be a multiple of the USB endpoint MaxPacketSize.
    // Typical MaxPacketSize values for USB 2.0 and 3.0 are 512 and 1024.
    private const int TransferBufferSize = 32 * 1024;

    private readonly ILogger<UsbInterface> _logger;
    private readonly UsbDevice _device;
    private readonly IUsbInterfaceDescriptor _descriptor;
    private readonly ISafeDeviceInterface _claimedInterface;
    private readonly Lazy<IUsbEndpointDescriptor> _bulkReadEndpoint;
    private readonly Lazy<IUsbEndpointDescriptor> _bulkWriteEndpoint;

    /// <summary>
    /// Pinned transfer buffers keyed by endpoint address, allocated on the first transfer to an
    /// endpoint. Lock the dictionary to access it. The buffers are only used and freed while
    /// _disposeLock is held; by transfers as readers and by Dispose as the writer.
    /// </summary>
    private readonly Dictionary<byte, EndpointBuffer> _endpointBuffers = [];

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

    /// <inheritdoc/>
    public byte AlternateSetting => _descriptor.AlternateSetting;

    /// <summary>
    /// A type representing a claimed USB interface.
    /// </summary>
    /// <param name="logger">Logger for this UsbInterface.</param>
    /// <param name="device">The parent USB device.</param>
    /// <param name="descriptor">The USB interface descriptor.</param>
    /// <param name="claimedInterface">A claimed USB device interface.</param>
    /// <param name="readEndpoint">
    /// Optional bulk input endpoint. When nothing is specified and a read operation is attempted,
    /// an attempt is made to pick the first available bulk input endpoint for this interface.
    /// </param>
    /// <param name="writeEndpoint">
    /// Optional bulk output endpoint. When nothing is specified and a write operation is attempted,
    /// an attempt is made to pick the first available bulk output endpoint for this interface.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="readEndpoint"/> is not a bulk input endpoint, or when
    /// <paramref name="writeEndpoint"/> is not a bulk output endpoint.
    /// </exception>
    public UsbInterface(
        ILogger<UsbInterface> logger,
        UsbDevice device,
        IUsbInterfaceDescriptor descriptor,
        ISafeDeviceInterface claimedInterface,
        IUsbEndpointDescriptor? readEndpoint = default,
        IUsbEndpointDescriptor? writeEndpoint = default
    )
    {
        CheckBulkEndpoint(readEndpoint, UsbEndpointDirection.Input, nameof(readEndpoint));
        CheckBulkEndpoint(writeEndpoint, UsbEndpointDirection.Output, nameof(writeEndpoint));
        _logger = logger;
        _device = device;
        _descriptor = descriptor;
        _claimedInterface = claimedInterface;
        _bulkReadEndpoint = readEndpoint is null
            ? new Lazy<IUsbEndpointDescriptor>(() =>
                GetBulkEndpoint(descriptor, UsbEndpointDirection.Input)
            )
            : new Lazy<IUsbEndpointDescriptor>(readEndpoint);
        _bulkWriteEndpoint = writeEndpoint is null
            ? new Lazy<IUsbEndpointDescriptor>(() =>
                GetBulkEndpoint(descriptor, UsbEndpointDirection.Output)
            )
            : new Lazy<IUsbEndpointDescriptor>(writeEndpoint);
        _disposeCts = new CancellationTokenSource();
    }

    /// <inheritdoc/>
    public bool TryGetInputEndpoint(
        [NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint,
        UsbEndpointTransferType transferType
    ) => TryGetEndpoint(UsbEndpointDirection.Input, transferType, out endpoint);

    /// <inheritdoc/>
    public bool TryGetOutputEndpoint(
        [NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint,
        UsbEndpointTransferType transferType
    ) => TryGetEndpoint(UsbEndpointDirection.Output, transferType, out endpoint);

    private bool TryGetEndpoint(
        UsbEndpointDirection direction,
        UsbEndpointTransferType transferType,
        [NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint
    )
    {
        if (transferType is not UsbEndpointTransferType.Bulk)
        {
            endpoint = FindEndpoints(_descriptor, direction, transferType).FirstOrDefault();
            return endpoint is not null;
        }
        // Return the endpoint that BulkRead/BulkWrite use
        try
        {
            endpoint =
                direction is UsbEndpointDirection.Input
                    ? _bulkReadEndpoint.Value
                    : _bulkWriteEndpoint.Value;
            return true;
        }
        catch (InvalidOperationException)
        {
            endpoint = null;
            return false;
        }
    }

    private static IEnumerable<IUsbEndpointDescriptor> FindEndpoints(
        IUsbInterfaceDescriptor descriptor,
        UsbEndpointDirection direction,
        UsbEndpointTransferType transferType
    ) =>
        descriptor.Endpoints.Where(e =>
            e.EndpointAddress.Direction == direction && e.Attributes.TransferType == transferType
        );

    /// <inheritdoc/>
    public UsbResult Read(
        IUsbEndpointDescriptor endpoint,
        Span<byte> destination,
        out int bytesRead,
        int timeout
    ) =>
        ExecuteRead(
            GetInterfaceEndpoint(endpoint, UsbEndpointDirection.Input),
            destination,
            out bytesRead,
            timeout
        );

    /// <inheritdoc/>
    public UsbResult Write(
        IUsbEndpointDescriptor endpoint,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        int timeout
    ) =>
        ExecuteWrite(
            GetInterfaceEndpoint(endpoint, UsbEndpointDirection.Output),
            source,
            out bytesWritten,
            timeout
        );

    /// <inheritdoc/>
    public UsbResult BulkRead(Span<byte> destination, out int bytesRead, int timeout) =>
        // The default bulk input endpoint is resolved lazily, on the first read
        ExecuteRead(_bulkReadEndpoint.Value, destination, out bytesRead, timeout);

    /// <inheritdoc/>
    public UsbResult BulkWrite(ReadOnlySpan<byte> source, out int bytesWritten, int timeout) =>
        // The default bulk output endpoint is resolved lazily, on the first write
        ExecuteWrite(_bulkWriteEndpoint.Value, source, out bytesWritten, timeout);

    /// <inheritdoc/>
    public void ClearHalt(IUsbEndpointDescriptor endpoint)
    {
        var interfaceEndpoint = FindInterfaceEndpoint(endpoint);
        var address = interfaceEndpoint.EndpointAddress;
        EnterTransferLock();
        try
        {
            // One operation at a time per endpoint: wait for a transfer in progress, and keep a
            // new one from starting, since the halt must not be cleared with a transfer pending
            lock (GetEndpointBuffer(interfaceEndpoint))
            {
                _device.Handle.ClearHalt(address.RawValue);
            }
        }
        catch (LibUsbException ex)
        {
            throw new UsbException(
                ex.Code,
                $"Failed to clear halt on endpoint {address} of interface {this}.",
                ex
            );
        }
        finally
        {
            _disposeLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Read from a bulk or interrupt input endpoint of this interface.
    /// </summary>
    private UsbResult ExecuteRead(
        IUsbEndpointDescriptor endpoint,
        Span<byte> destination,
        out int bytesRead,
        int timeout
    )
    {
        TransferTimeout.ThrowIfInvalid(timeout);
        try
        {
            EnterTransferLock();
            try
            {
                var maxPacketSize = endpoint.MaxPacketSize;
                CheckDestinationSpanLength(destination, maxPacketSize);
                var bufferLength = Math.Min(destination.Length, TransferBufferSize);
                if (maxPacketSize > 0)
                {
                    // Keep the buffer length a multiple of the max packet size when it is capped
                    bufferLength -= bufferLength % maxPacketSize;
                }
                var buffer = GetEndpointBuffer(endpoint);
                // One transfer at a time per endpoint; they share the endpoint buffer
                lock (buffer)
                {
                    var result = LibUsbTransfer.ExecuteSync(
                        _logger,
                        _device.Handle,
                        ToLibUsbTransferType(endpoint.Attributes.TransferType),
                        endpoint.EndpointAddress.RawValue,
                        buffer.Handle,
                        bufferLength,
                        timeout > 0 ? (uint)timeout : 0,
                        out bytesRead,
                        _disposeCts.Token
                    );
                    if (bytesRead > 0)
                    {
                        buffer.Bytes.AsSpan(0, bytesRead).CopyTo(destination);
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
                "Read interrupted. {ErrorType}: {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
            bytesRead = 0;
            return UsbResult.Interrupted;
        }
    }

    /// <summary>
    /// Write to a bulk or interrupt output endpoint of this interface.
    /// </summary>
    private UsbResult ExecuteWrite(
        IUsbEndpointDescriptor endpoint,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        int timeout
    )
    {
        TransferTimeout.ThrowIfInvalid(timeout);
        try
        {
            EnterTransferLock();
            try
            {
                var bufferLength = Math.Min(source.Length, TransferBufferSize);
                var buffer = GetEndpointBuffer(endpoint);
                // One transfer at a time per endpoint; they share the endpoint buffer
                lock (buffer)
                {
                    source[..bufferLength].CopyTo(buffer.Bytes.AsSpan(0, bufferLength));
                    return LibUsbTransfer
                        .ExecuteSync(
                            _logger,
                            _device.Handle,
                            ToLibUsbTransferType(endpoint.Attributes.TransferType),
                            endpoint.EndpointAddress.RawValue,
                            buffer.Handle,
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
                "Write interrupted. {ErrorType}: {ErrorMessage}",
                ex.GetType().Name,
                ex.Message
            );
            bytesWritten = 0;
            return UsbResult.Interrupted;
        }
    }

    /// <summary>
    /// Enter the dispose lock as a reader; held for the duration of a transfer. A read lock is
    /// used for both reads and writes, to support duplex. Exit with _disposeLock.ExitReadLock().
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when dispose has started.</exception>
    private void EnterTransferLock()
    {
        lock (_disposeLockSync)
        {
            // Try enter the lock with a zero timeout. The write lock is only taken by Dispose,
            // so failing to enter means dispose has started. Never block on _disposeLock while
            // holding _disposeLockSync, since Dispose() needs it to exit the write lock.
            if (_disposed || !_disposeLock.TryEnterReadLock(0))
            {
                throw new ObjectDisposedException(nameof(UsbInterface));
            }
        }
    }

    /// <summary>
    /// Get the pinned transfer buffer of an endpoint; allocated on first use, freed by Dispose.
    /// Only call this while holding the transfer lock, see <see cref="EnterTransferLock"/>.
    /// </summary>
    private EndpointBuffer GetEndpointBuffer(IUsbEndpointDescriptor endpoint)
    {
        var address = endpoint.EndpointAddress.RawValue;
        lock (_endpointBuffers)
        {
            if (!_endpointBuffers.TryGetValue(address, out var buffer))
            {
                buffer = new EndpointBuffer(TransferBufferSize);
                _endpointBuffers.Add(address, buffer);
            }
            return buffer;
        }
    }

    /// <summary>
    /// Find the endpoint of this interface with the same address as the given endpoint, and
    /// check that it can be used for a transfer in the given direction.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the endpoint is not an endpoint of this interface, or has another direction.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the transfer type of the endpoint is not supported.
    /// </exception>
    private IUsbEndpointDescriptor GetInterfaceEndpoint(
        IUsbEndpointDescriptor endpoint,
        UsbEndpointDirection direction
    )
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var address = endpoint.EndpointAddress;
        if (address.Direction != direction)
        {
            throw new ArgumentException(
                $"Invalid endpoint {address}; must be an {direction} endpoint.",
                nameof(endpoint)
            );
        }
        var interfaceEndpoint = FindInterfaceEndpoint(endpoint);
        _ = ToLibUsbTransferType(interfaceEndpoint.Attributes.TransferType);
        return interfaceEndpoint;
    }

    /// <summary>
    /// Find the endpoint of this interface with the same address as the given endpoint.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the endpoint is not an endpoint of this interface.
    /// </exception>
    private IUsbEndpointDescriptor FindInterfaceEndpoint(IUsbEndpointDescriptor endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var address = endpoint.EndpointAddress;
        // Use the descriptor of this interface, not the one provided by the caller
        return _descriptor.Endpoints.FirstOrDefault(e =>
                e.EndpointAddress.RawValue == address.RawValue
            )
            ?? throw new ArgumentException(
                $"Invalid endpoint {address}; not an endpoint of interface {this}.",
                nameof(endpoint)
            );
    }

    /// <summary>
    /// Map the transfer type of an endpoint to the libusb transfer type to submit to it.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown for control and isochronous endpoints, which IUsbInterface can not transfer to.
    /// </exception>
    private static libusb_endpoint_transfer_type ToLibUsbTransferType(
        UsbEndpointTransferType transferType
    ) =>
        transferType switch
        {
            UsbEndpointTransferType.Bulk =>
                libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK,
            UsbEndpointTransferType.Interrupt =>
                libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_INTERRUPT,
            UsbEndpointTransferType.Control => throw new NotSupportedException(
                "Control transfers are not supported on an interface; use the device."
            ),
            UsbEndpointTransferType.Isochronous => throw new NotSupportedException(
                "Isochronous transfers are not supported."
            ),
        };

    /// <inheritdoc/>
    public override string ToString() =>
        $"#{_descriptor.InterfaceNumber} "
        + $"({_descriptor.InterfaceClass} subclass {_descriptor.InterfaceSubClass})";

    private IUsbEndpointDescriptor GetBulkEndpoint(
        IUsbInterfaceDescriptor descriptor,
        UsbEndpointDirection direction
    )
    {
        // Only bulk endpoints are candidates; BulkRead and BulkWrite submit bulk transfers,
        // which must not be sent to an interrupt or isochronous endpoint.
        var endpoints = FindEndpoints(descriptor, direction, UsbEndpointTransferType.Bulk).ToList();
        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException(
                $"Interface #{descriptor.InterfaceNumber} has no bulk {direction} endpoint."
            );
        }
        if (endpoints.Count > 1)
        {
            _logger.LogWarning(
                "Interface #{InterfaceNumber} has {EndpointCount} bulk {EndpointDirection} "
                    + "endpoints. The first endpoint was selected.",
                descriptor.InterfaceNumber,
                endpoints.Count,
                direction
            );
        }
        return endpoints[0];
    }

    /// <summary>
    /// Throw ArgumentException when an explicitly provided endpoint is not a bulk endpoint with
    /// the expected direction. BulkRead and BulkWrite submit bulk transfers to these endpoints.
    /// </summary>
    private static void CheckBulkEndpoint(
        IUsbEndpointDescriptor? endpoint,
        UsbEndpointDirection direction,
        string paramName
    )
    {
        if (
            endpoint is not null
            && (
                endpoint.EndpointAddress.Direction != direction
                || endpoint.Attributes.TransferType != UsbEndpointTransferType.Bulk
            )
        )
        {
            throw new ArgumentException(
                $"Invalid endpoint {endpoint.EndpointAddress} with transfer type "
                    + $"{endpoint.Attributes.TransferType}; must be a bulk {direction} endpoint.",
                paramName
            );
        }
    }

    /// <summary>
    /// Throw ArgumentException when the read buffer length is not a non-zero multiple of the
    /// endpoint max packet size. Such a read works on some operating systems, but on others it
    /// fails with an overflow and loses data when the device sends more than the requested length.
    /// </summary>
    private static void CheckDestinationSpanLength(Span<byte> destination, ushort maxPacketSize)
    {
        // A max packet size of zero means that the size is not known, the length is not checked
        var length = destination.Length;
        if (maxPacketSize > 0 && (length == 0 || length % maxPacketSize != 0))
        {
            throw new ArgumentException(
                $"Invalid read buffer length {length}; must be a non-zero multiple of the endpoint "
                    + $"max packet size ({maxPacketSize}).",
                nameof(destination)
            );
        }
    }

    /// <summary>
    /// Disposes this interface and associated resources. Ongoing read and write
    /// transfers are canceled and allocated transfer buffers are freed.
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
                // Free the endpoint transfer buffers
                lock (_endpointBuffers)
                {
                    foreach (var buffer in _endpointBuffers.Values)
                    {
                        buffer.Handle.Free();
                    }
                    _endpointBuffers.Clear();
                }
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

    /// <summary>
    /// A pinned transfer buffer for one endpoint. Lock the instance for the duration of a
    /// transfer; libusb reads from or writes to the buffer until the transfer completes.
    /// </summary>
    private sealed class EndpointBuffer
    {
        public byte[] Bytes { get; }

        public GCHandle Handle { get; }

        public EndpointBuffer(int size)
        {
            Bytes = new byte[size];
            Handle = GCHandle.Alloc(Bytes, GCHandleType.Pinned);
        }
    }
}
