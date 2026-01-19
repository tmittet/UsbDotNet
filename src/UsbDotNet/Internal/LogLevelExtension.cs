using Microsoft.Extensions.Logging;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.Internal;

internal static class LogLevelExtension
{
    internal static libusb_log_level ToLibUsbLogLevel(this LogLevel logLevel) =>
        logLevel switch
        {
            // LibUsbLogLevel.Debug is very verbose and is best mapped to .NET LogLevel.Trace
            LogLevel.Trace => libusb_log_level.LIBUSB_LOG_LEVEL_DEBUG,
            LogLevel.Debug => libusb_log_level.LIBUSB_LOG_LEVEL_INFO,
            LogLevel.Information => libusb_log_level.LIBUSB_LOG_LEVEL_INFO,
            LogLevel.Warning => libusb_log_level.LIBUSB_LOG_LEVEL_WARNING,
            LogLevel.Error => libusb_log_level.LIBUSB_LOG_LEVEL_ERROR,
            LogLevel.Critical => libusb_log_level.LIBUSB_LOG_LEVEL_ERROR,
            LogLevel.None => libusb_log_level.LIBUSB_LOG_LEVEL_NONE,
        };
}
