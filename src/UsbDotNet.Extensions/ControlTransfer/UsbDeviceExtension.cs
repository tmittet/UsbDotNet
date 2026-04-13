using UsbDotNet.Core;
using UsbDotNet.Transfer;

namespace UsbDotNet.Extensions.ControlTransfer;

public static class UsbDeviceExtension
{
    /// <summary>
    /// Send a standard ControlRead request. Data is read Device -> Host.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="destination">A destination span for read bytes</param>
    /// <param name="bytesRead">The number of bytes read</param>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="request">The USB standard control request type</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the destination buffer is too large.</exception>
    /// <returns>
    /// <list>
    /// <item>Success = The read operation completed successfully.</item>
    /// <item>IO = The read operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>
    /// PipeError = Halt condition detected (endpoint stalled) or control request not supported.
    /// </item>
    /// <item>Timeout = The read operation timed out.</item>
    /// <item>Overflow = The device sent more data than expected.</item>
    /// <item>Interrupted = The read operation was canceled.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    public static UsbResult ControlRead(
        this IUsbDevice device,
        Span<byte> destination,
        out ushort bytesRead,
        ControlRequestRecipient recipient,
        StandardRequest request,
        ushort value,
        ushort index = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlRead(
            destination,
            out bytesRead,
            recipient,
            ControlRequestType.Standard,
            (byte)request,
            value,
            index,
            timeout
        );

    /// <summary>
    /// Send a standard ControlWrite request. Data is written Host -> Device.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="source">The payload to send to the device (max. 65_535 bytes)</param>
    /// <param name="bytesWritten">The actual number of bytes written to the device</param>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="request">The USB standard control request type</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the source payload is too large.</exception>
    /// <returns>
    /// <list>
    /// <item>Success = The write operation completed successfully.</item>
    /// <item>IO = The write operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>
    /// PipeError = Halt condition detected (endpoint stalled) or control request not supported.
    /// </item>
    /// <item>Timeout = The write operation timed out.</item>
    /// <item>Overflow = The host sent more data than expected.</item>
    /// <item>Interrupted = The write operation was canceled.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
    /// </returns>
    public static UsbResult ControlWrite(
        this IUsbDevice device,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        ControlRequestRecipient recipient,
        StandardRequest request,
        ushort value,
        ushort index = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlWrite(
            source,
            out bytesWritten,
            recipient,
            ControlRequestType.Standard,
            (byte)request,
            value,
            index,
            timeout
        );
}
