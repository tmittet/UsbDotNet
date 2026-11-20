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
    /// The alternate setting that was selected when this interface was claimed.
    /// </summary>
    byte AlternateSetting { get; }

    /// <summary>
    /// Get an input endpoint of the given transfer type, if one exists. If more than one is found,
    /// the first is returned. For the bulk transfer type, this is the endpoint used by
    /// <see cref="BulkRead"/>.
    /// </summary>
    /// <param name="endpoint">The endpoint descriptor, when one is found.</param>
    /// <param name="transferType">
    /// The transfer type of the endpoint: bulk, interrupt or isochronous. Control endpoints are
    /// not part of an interface, so no endpoint is found for the control transfer type.
    /// </param>
    bool TryGetInputEndpoint(
        [NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint,
        UsbEndpointTransferType transferType
    );

    /// <summary>
    /// Get an output endpoint of the given transfer type, if one exists. If more than one is
    /// found, the first is returned. For the bulk transfer type, this is the endpoint used by
    /// <see cref="BulkWrite"/>.
    /// </summary>
    /// <param name="endpoint">The endpoint descriptor, when one is found.</param>
    /// <param name="transferType">
    /// The transfer type of the endpoint: bulk, interrupt or isochronous. Control endpoints are
    /// not part of an interface, so no endpoint is found for the control transfer type.
    /// </param>
    bool TryGetOutputEndpoint(
        [NotNullWhen(true)] out IUsbEndpointDescriptor? endpoint,
        UsbEndpointTransferType transferType
    );

    /// <summary>
    /// Read data from a bulk or interrupt input endpoint of this interface. This method blocks
    /// until a chunk of data is received or the optional timeout is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this method to read from an interrupt endpoint, or from a specific endpoint when the
    /// interface has more than one input endpoint. <see cref="BulkRead"/> is a shorthand for a
    /// read from the first bulk input endpoint. The remarks on <see cref="BulkRead"/> about
    /// transfer completion, data received with a non Success result, the required length of
    /// <paramref name="destination"/> and the 32 KB limit also apply to this method.
    /// </para>
    /// <para>
    /// One transfer at a time is in progress on an endpoint; a concurrent read from the same
    /// endpoint waits for it to complete. Transfers on different endpoints run concurrently.
    /// </para>
    /// </remarks>
    /// <param name="endpoint">
    /// A bulk or interrupt input endpoint of this interface, from the descriptor of the interface
    /// or from <see cref="TryGetInputEndpoint"/>. The endpoint is identified by its address.
    /// </param>
    /// <param name="destination">
    /// A destination data span for read bytes. The length is the max number of bytes to read.
    /// </param>
    /// <param name="bytesRead">
    /// The number of bytes read. May be greater than zero when the result is not Success.
    /// </param>
    /// <param name="timeout">An optional timeout for the read operation</param>
    /// <exception cref="ArgumentNullException">Thrown when the endpoint is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the endpoint is not an input endpoint of this interface, when timeout is
    /// invalid, or when the length of <paramref name="destination"/> is not a non-zero multiple
    /// of the endpoint max packet size.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the endpoint is an isochronous endpoint.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The read operation completed successfully.</item>
    /// <item>IO = The read operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled); see ClearHalt.</item>
    /// <item>Timeout = The read operation timed out. Data may still have been received.</item>
    /// <item>Overflow = The device sent more data than expected.</item>
    /// <item>
    /// Interrupted = The read operation was canceled, or the interface is disposed. Data may
    /// still have been received.
    /// </item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult Read(
        IUsbEndpointDescriptor endpoint,
        Span<byte> destination,
        out int bytesRead,
        int timeout = Timeout.Infinite
    );

    /// <summary>
    /// Write data to a bulk or interrupt output endpoint of this interface. This method blocks
    /// until a chunk of data has been written or the optional timeout is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this method to write to an interrupt endpoint, or to a specific endpoint when the
    /// interface has more than one output endpoint. <see cref="BulkWrite"/> is a shorthand for a
    /// write to the first bulk output endpoint. The remarks on <see cref="BulkWrite"/> about
    /// the 32 KB limit, partially written data and zero length packets also apply to this method.
    /// </para>
    /// <para>
    /// One transfer at a time is in progress on an endpoint; a concurrent write to the same
    /// endpoint waits for it to complete. Transfers on different endpoints run concurrently.
    /// </para>
    /// </remarks>
    /// <param name="endpoint">
    /// A bulk or interrupt output endpoint of this interface, from the descriptor of the
    /// interface or from <see cref="TryGetOutputEndpoint"/>. The endpoint is identified by its
    /// address.
    /// </param>
    /// <param name="source">A source span of data to write</param>
    /// <param name="bytesWritten">
    /// The number of bytes written. May be less than the length of <paramref name="source"/>.
    /// </param>
    /// <param name="timeout">An optional timeout for the write operation</param>
    /// <exception cref="ArgumentNullException">Thrown when the endpoint is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the endpoint is not an output endpoint of this interface, or when timeout is
    /// invalid.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the endpoint is an isochronous endpoint.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The write operation completed successfully.</item>
    /// <item>IO = The write operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled); see ClearHalt.</item>
    /// <item>Timeout = The write operation timed out.</item>
    /// <item>Overflow = The host sent more data than expected.</item>
    /// <item>Interrupted = The write operation was canceled, or the interface is disposed.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult Write(
        IUsbEndpointDescriptor endpoint,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        int timeout = Timeout.Infinite
    );

    /// <summary>
    /// Bulk read data from the USB device interface. This method blocks until a chunk of data is
    /// received or the optional timeout is reached. Under the hood it submits a libusb transfer,
    /// then waits for a transfer completion, timeout or an error callback to be received.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bulk read completes when the device ends the transfer with a short packet (a packet
    /// smaller than the endpoint max packet size, including a zero length packet) or when the
    /// requested number of bytes has been received. When the device sends an exact multiple of
    /// the max packet size without a terminating zero length packet, and fewer bytes than the
    /// length of <paramref name="destination"/>, the read does not complete until it times out.
    /// To avoid waiting for the timeout, request exactly the number of bytes that are expected.
    /// This is how USB bulk transfers work; it is not specific to libusb or an operating system.
    /// </para>
    /// <para>
    /// A read that returns Timeout, Interrupted or another error may still have received data.
    /// Always check <paramref name="bytesRead"/>, also when the result is not Success. A timeout
    /// that occurs at the same moment as data arrives, may return both Timeout and the data.
    /// </para>
    /// <para>
    /// The length of <paramref name="destination"/> must be a non-zero multiple of the endpoint
    /// max packet size, see <see cref="TryGetInputEndpoint"/>. A read of any other length works
    /// on some operating systems, but on others it fails with Overflow and loses data when the
    /// device sends more than requested; it is rejected on all of them. At most 32 KB is read
    /// in a single call; a longer <paramref name="destination"/> is not filled.
    /// </para>
    /// </remarks>
    /// <param name="destination">
    /// A destination data span for read bytes. The length is the max number of bytes to read.
    /// </param>
    /// <param name="bytesRead">
    /// The number of bytes read. May be greater than zero when the result is not Success.
    /// </param>
    /// <param name="timeout">An optional timeout for the read operation</param>
    /// <exception cref="ArgumentException">
    /// Thrown when timeout is invalid, or when the length of <paramref name="destination"/> is not
    /// a non-zero multiple of the endpoint max packet size.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no bulk input endpoint is found for this interface.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The read operation completed successfully.</item>
    /// <item>IO = The read operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled); see ClearHalt.</item>
    /// <item>Timeout = The read operation timed out. Data may still have been received.</item>
    /// <item>Overflow = The device sent more data than expected.</item>
    /// <item>
    /// Interrupted = The read operation was canceled, or the interface is disposed. Data may
    /// still have been received.
    /// </item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult BulkRead(Span<byte> destination, out int bytesRead, int timeout = Timeout.Infinite);

    /// <summary>
    /// Bulk write data to the USB device interface. This method blocks until a chunk of data has
    /// been written or the timeout is reached. Under the hood it submits a libusb transfer, then
    /// waits for the transfer completed, timeout or error callback to be received.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At most 32 KB is written in a single call. When <paramref name="source"/> is longer, the
    /// result is Success and <paramref name="bytesWritten"/> is less than the source length; call
    /// again with the remaining data. A write that returns Timeout or another error may also have
    /// written some of the data, always check <paramref name="bytesWritten"/>.
    /// </para>
    /// <para>
    /// No zero length packet is added when the length of <paramref name="source"/> is a multiple
    /// of the endpoint max packet size. A device that reads more bytes than it is sent, will not
    /// see the end of such a transfer. Write an empty <paramref name="source"/> to send a zero
    /// length packet, if the device protocol requires one.
    /// </para>
    /// </remarks>
    /// <param name="source">A source span of data to write</param>
    /// <param name="bytesWritten">
    /// The number of bytes written. May be less than the length of <paramref name="source"/>.
    /// </param>
    /// <param name="timeout">A timeout for the write operation</param>
    /// <exception cref="ArgumentException">Thrown when the timeout is invalid.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no bulk output endpoint is found for this interface.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The write operation completed successfully.</item>
    /// <item>IO = The write operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>PipeError = Halt condition detected (endpoint stalled); see ClearHalt.</item>
    /// <item>Timeout = The write operation timed out.</item>
    /// <item>Overflow = The host sent more data than expected.</item>
    /// <item>Interrupted = The write operation was canceled, or the interface is disposed.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    UsbResult BulkWrite(
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        int timeout = Timeout.Infinite
    );

    /// <summary>
    /// Clears the halt (stall) condition of an endpoint of this interface. A halted endpoint is
    /// unable to receive or transmit data, and its transfers return PipeError, until the halt
    /// condition is cleared.
    /// <para>
    /// Waits for a transfer that is in progress on the endpoint to complete. Do not call it while
    /// a transfer without a timeout is waiting for data on the endpoint.
    /// </para>
    /// <para>
    /// Clearing the halt condition makes the endpoint usable again; it does not remove the cause.
    /// What made the device stall the endpoint, and what state the device expects afterwards, is
    /// defined by the protocol of the device.
    /// </para>
    /// </summary>
    /// <param name="endpoint">
    /// An endpoint of this interface, typically from the interface descriptor that was used to
    /// claim the interface, or from <see cref="TryGetInputEndpoint"/> or
    /// <see cref="TryGetOutputEndpoint"/>. The endpoint is identified by its address.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the endpoint is not an endpoint of this interface.
    /// </exception>
    /// <exception cref="UsbException">Thrown when the clear halt operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbInterface is disposed.</exception>
    void ClearHalt(IUsbEndpointDescriptor endpoint);
}
