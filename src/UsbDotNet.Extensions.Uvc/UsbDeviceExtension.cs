using UsbDotNet.Core;
using UsbDotNet.Extensions.Uvc.Unix;
using UsbDotNet.Extensions.Uvc.Windows;
using UsbDotNet.Transfer;

namespace UsbDotNet.Extensions.Uvc;

public static class UsbDeviceExtension
{
    /// <summary>
    /// Send a UVC ControlRead request. Data is read Device -> Host.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="destination">A destination span for read bytes</param>
    /// <param name="bytesRead">The number of bytes read</param>
    /// <param name="request">The UVC control request type</param>
    /// <param name="interfaceNumber">The InterfaceNumber from the USB configuration descriptor</param>
    /// <param name="entityId">The unit, terminal or interface within the video function</param>
    /// <param name="control">The target control within the entity, e.g. brightness or zoom</param>
    /// <param name="fieldNumber">The channel or field number of the control, e.g. right channel</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the destination buffer is too large.</exception>
    /// <returns>
    /// Success = The read operation completed successfully.<br />
    /// IO = The read operation failed.<br />
    /// InvalidParameter = The transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The read operation timed out.<br />
    /// Overflow = The device sent more data than expected.<br />
    /// Interrupted = The read operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    public static UsbResult ControlReadUvc(
        this IUsbDevice device,
        Span<byte> destination,
        out ushort bytesRead,
        UvcControlRequest request,
        byte interfaceNumber,
        byte entityId,
        byte control,
        byte fieldNumber = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlRead(
            destination,
            out bytesRead,
            ControlRequestRecipient.Interface,
            ControlRequestType.Class,
            (byte)request,
            (ushort)(control << 8 | fieldNumber),
            (ushort)(entityId << 8 | interfaceNumber),
            timeout
        );

    /// <summary>
    /// Send a UVC ControlWrite request. Data is written Host -> Device.
    /// </summary>
    /// <param name="device">A UsbDevice instance</param>
    /// <param name="source">The payload to send to the device (max. 65.535 bytes)</param>
    /// <param name="bytesWritten">The actual number of bytes written to the device</param>
    /// <param name="request">The UVC control request type</param>
    /// <param name="interfaceNumber">The InterfaceNumber from the USB configuration descriptor</param>
    /// <param name="entityId">The unit, terminal or interface within the video function</param>
    /// <param name="control">The target control within the entity, e.g. brightness or zoom</param>
    /// <param name="fieldNumber">The channel or field number of the control, e.g. right channel</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the source payload is too large.</exception>
    /// <returns>
    /// Success = The write operation completed successfully.<br />
    /// IO = The write operation failed.<br />
    /// InvalidParameter = The transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The write operation timed out.<br />
    /// Overflow = The host sent more data than expected.<br />
    /// Interrupted = The write operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    public static UsbResult ControlWriteUvc(
        this IUsbDevice device,
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        UvcControlRequest request,
        byte interfaceNumber,
        byte entityId,
        byte control,
        byte fieldNumber = 0,
        int timeout = Timeout.Infinite
    ) =>
        device.ControlWrite(
            source,
            out bytesWritten,
            ControlRequestRecipient.Interface,
            ControlRequestType.Class,
            (byte)request,
            (ushort)(control << 8 | fieldNumber),
            (ushort)(entityId << 8 | interfaceNumber),
            timeout
        );

    /// <summary>
    /// Opens cross-platform access to the specified UVC control interface.
    /// </summary>
    /// <remarks>
    /// On Windows, each Open method enumerates DirectShow video devices to obtain a
    /// <see cref="SafeVideoDeviceHandle"/>. The device must be open so the serial number can be read.
    /// On Linux and macOS the USB device is accessed directly via libusb control transfers.
    /// </remarks>
    /// <param name="device">An open USB device.</param>
    /// <param name="interfaceNumber">
    /// The UVC VideoControl interface number from the device configuration descriptor.
    /// </param>
    /// <returns>
    /// An <see cref="IUvcControls"/> bound to the specified interface,
    /// backed by Kernel Streaming on Windows or by libusb UVC control transfers on Linux and macOS.
    /// </returns>
    /// <remarks>
    /// On Windows: If possible call <see cref="OpenUvcControls(IUsbDevice, byte)"/> from an STA
    /// (Single-Threaded Apartment) thread, as DirectShow components are apartment-threaded.
    /// Alternatively, make sure the thread calling OpenUvcControls lives as long as the lifetime
    /// of the returned <see cref="IUvcControls"/> instance. If not, DirectShow calls may fail.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// On Windows: no matching DirectShow video device found.
    /// </exception>
    public static IUvcControls OpenUvcControls(this IUsbDevice device, byte interfaceNumber)
    {
        ArgumentNullException.ThrowIfNull(device);
        return OperatingSystem.IsWindows()
            ? new WindowsUvcControls(SafeVideoDeviceHandle.Open(device, interfaceNumber))
            : new UnixUvcControls(device, interfaceNumber);
    }
}
