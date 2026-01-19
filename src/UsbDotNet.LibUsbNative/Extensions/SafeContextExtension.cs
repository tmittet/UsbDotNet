using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Extensions;

public static class SafeContextExtension
{
    public static void SetOption(this ISafeContext safeContext, libusb_log_level value) =>
        safeContext.SetOption(libusb_option.LIBUSB_OPTION_LOG_LEVEL, (int)value);
}
