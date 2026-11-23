using UsbDotNet.Core;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative;

/// <summary>
/// An exception for a failed libusb operation, with the error that libusb reported.
/// </summary>
public class LibUsbException : UsbException
{
    /// <summary>The error that libusb reported.</summary>
    public libusb_error Error { get; }

    /// <summary>Creates an exception for the given libusb error.</summary>
    public LibUsbException(libusb_error error, string message)
        : base(error.ToUsbResult(), message)
    {
        Error = error;
    }

    /// <summary>
    /// Creates an exception for the given libusb error, caused by another exception.
    /// </summary>
    public LibUsbException(libusb_error error, string message, Exception? innerException)
        : base(error.ToUsbResult(), message, innerException)
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
