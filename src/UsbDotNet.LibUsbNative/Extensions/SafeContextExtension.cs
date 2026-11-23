using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Extensions;

public static class SafeContextExtension
{
    /// <summary>
    /// Set the libusb log level of the context.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeContext is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the set option operation fails.</exception>
    public static void SetLogLevel(this ISafeContext safeContext, libusb_log_level value) =>
        safeContext.SetOption(libusb_option.LIBUSB_OPTION_LOG_LEVEL, (int)value);
}
