using LibUsbSharp.Transfer;

namespace LibUsbSharp.Extensions.ControlTransfer.Uvc;

public static class UsbDeviceExtension
{
    /// <summary>
    /// Send a UVC ControlRead request. Data is read Device -> Host.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="destination">A destination span for read bytes</param>
    /// <param name="bytesRead">The number of bytes read</param>
    /// <param name="request">The USB standard control request type</param>
    /// <param name="interfaceNumber">The InterfaceNumber from the USB configuration descriptor</param>
    /// <param name="entityId">The unit, terminal or interface within the video function</param>
    /// <param name="controlSelector">The target control within the entity, e.g. brightness or zoom</param>
    /// <param name="fieldNumber">The channel or field number of the control, e.g. right channel</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the destination buffer is too large.</exception>
    /// <returns>
    /// Success = The read operation completed successfully.<br />
    /// IO = The read operation failed.<br />
    /// InvalidParameter = Transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The read operation timed out.<br />
    /// Overflow = The device sent more data than expected.<br />
    /// Interrupted = The read operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    public static LibUsbResult ControlReadUvc(
        this IUsbDevice device,
        Span<byte> destination,
        out ushort bytesRead,
        ControlRequestUvc request,
        byte interfaceNumber,
        byte entityId,
        byte controlSelector,
        byte fieldNumber = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlRead(
            destination,
            out bytesRead,
            ControlRequestRecipient.Interface,
            ControlRequestType.Class,
            (byte)request,
            (ushort)(controlSelector << 8 | fieldNumber),
            (ushort)(entityId << 8 | interfaceNumber),
            timeout
        );

    /// <summary>
    /// Send a UVC ControlWrite request. Data is written Host -> Device.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="source">The payload to send to the device (max. 65.535 bytes)</param>
    /// <param name="bytesWritten">The actual number of bytes written to the device</param>
    /// <param name="request">The USB standard control request type</param>
    /// <param name="interfaceNumber">The InterfaceNumber from the USB configuration descriptor</param>
    /// <param name="entityId">The unit, terminal or interface within the video function</param>
    /// <param name="controlSelector">The target control within the entity, e.g. brightness or zoom</param>
    /// <param name="fieldNumber">The channel or field number of the control, e.g. right channel</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the source payload is too large.</exception>
    /// <returns>
    /// Success = The write operation completed successfully.<br />
    /// IO = The write operation failed.<br />
    /// InvalidParameter = Transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The write operation timed out.<br />
    /// Overflow = The host sent more data than expected.<br />
    /// Interrupted = The write operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    public static LibUsbResult ControlWriteUvc(
        this IUsbDevice device,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        ControlRequestUvc request,
        byte interfaceNumber,
        byte entityId,
        byte controlSelector,
        byte fieldNumber = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlWrite(
            source,
            out bytesWritten,
            ControlRequestRecipient.Interface,
            ControlRequestType.Class,
            (byte)request,
            (ushort)(controlSelector << 8 | fieldNumber),
            (ushort)(entityId << 8 | interfaceNumber),
            timeout
        );
}
