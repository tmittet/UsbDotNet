using UsbDotNet.Core;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative;

public class LibUsbException : UsbException
{
    public libusb_error Error { get; init; }

    public LibUsbException(libusb_error error, string message)
        : base(error.ToUsbResult(), message)
    {
        Error = error;
    }

    public LibUsbException()
        : this(null) { }

    public LibUsbException(string? message)
        : this(message, null) { }

    public LibUsbException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Error = libusb_error.LIBUSB_ERROR_OTHER;
    }
}
