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
}
