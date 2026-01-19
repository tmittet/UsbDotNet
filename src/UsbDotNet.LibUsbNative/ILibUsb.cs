using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative;

public interface ILibUsb
{
    ISafeContext CreateContext();

    libusb_version GetVersion();

    bool HasCapability(libusb_capability capability);

    string StrError(libusb_error usbError);
}
