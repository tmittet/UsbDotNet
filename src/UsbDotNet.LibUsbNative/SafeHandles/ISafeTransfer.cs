using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeTransfer : IDisposable
{
    /// <summary>
    /// Fire off a USB transfer and then return immediately.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeTransfer is disposed.</exception>
    libusb_error Submit();

    /// <summary>
    /// Asynchronously cancel a previously submitted transfer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeTransfer is disposed.</exception>
    libusb_error Cancel();

    /// <summary>
    /// Get a pointer to the transfer buffer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeTransfer is disposed.</exception>
    nint GetBufferPtr();

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
