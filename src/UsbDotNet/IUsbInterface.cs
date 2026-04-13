using System.Diagnostics.CodeAnalysis;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Represents a USB interface, providing methods for accessing endpoints and performing IO.
/// </summary>
public interface IUsbInterface : IDisposable
{
    /// <summary>
    /// The number of this interface.
    /// </summary>
    byte Number { get; }

    /// <summary>
    /// Get an input endpoint, if one exists. If more than one is found, the first is returned.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when UsbInterface is disposed</exception>
    bool TryGetInputEndpoint([NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint);

    /// <summary>
    /// Get an output endpoint, if one exists. If more than one is found, the first is returned.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when UsbInterface is disposed</exception>
    bool TryGetOutputEndpoint([NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint);

    /// <summary>
    /// Bulk read data from the USB device interface. This method blocks until a chunk of data is
    /// received or the optional timeout is reached. Under the hood it submits a libusb transfer,
    /// then waits for a transfer completion, timeout or an error callback to be received.
    /// </summary>
    /// <param name="destination">A destination data span for read bytes</param>
    /// <param name="bytesRead">The number of bytes read</param>
    /// <param name="timeout">An optional timeout for the read operation</param>
    /// <exception cref="ArgumentException">Thrown when timeout is invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when UsbInterface is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no input endpoint is found for this interface.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The read operation completed successfully.</item>
    /// <item>IO = The read operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled).</item>
    /// <item>Timeout = The read operation timed out.</item>
    /// <item>Overflow = The device sent more data than expected.</item>
    /// <item>Interrupted = The read operation was canceled.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult BulkRead(Span<byte> destination, out int bytesRead, int timeout = Timeout.Infinite);

    /// <summary>
    /// Bulk write data to the USB device interface. This method blocks until a chunk of data has
    /// been written or the timeout is reached. Under the hood it submits a libusb transfer, then
    /// waits for the transfer completed, timeout or error callback to be received.
    /// </summary>
    /// <param name="source">A source span of data to write</param>
    /// <param name="bytesWritten">The number of bytes written</param>
    /// <param name="timeout">A timeout for the write operation</param>
    /// <exception cref="ArgumentException">Thrown when the timeout is invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbInterface is disposed</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no output endpoint is found for this interface.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The write operation completed successfully.</item>
    /// <item>IO = The write operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled).</item>
    /// <item>Timeout = The write operation timed out.</item>
    /// <item>Overflow = The host sent more data than expected.</item>
    /// <item>Interrupted = The write operation was canceled.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult BulkWrite(
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        int timeout = Timeout.Infinite
    );
}
