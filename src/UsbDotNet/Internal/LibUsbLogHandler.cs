using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.Internal;

internal static class LibUsbLogHandler
{
    private static ILogger? _staticLogger;
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static void SetLogger(ILogger logger) => _staticLogger = logger;

    public static void ClearLogger() => _staticLogger = null;

    public static void Log(libusb_log_level level, string message)
    {
        var logger = _staticLogger;
        if (logger is null || level == libusb_log_level.LIBUSB_LOG_LEVEL_NONE)
        {
            return;
        }
        // Trim libusb log messages to remove trailing whitespace; typically newline characters
        message = message.TrimEnd();
        // Selectively lower the log level for messages that are too verbose
        if (LogAsTraceOverride(level, message))
        {
            logger.LogTrace("{LibUsbMessage}", message);
            return;
        }
        if (LogAsDebugOverride(level, message))
        {
            logger.LogDebug("{LibUsbMessage}", message);
            return;
        }
        // Map libusb log levels and log the message
        switch (level)
        {
            case libusb_log_level.LIBUSB_LOG_LEVEL_ERROR:
                logger.LogError("{LibUsbMessage}", message);
                break;
            case libusb_log_level.LIBUSB_LOG_LEVEL_WARNING:
                logger.LogWarning("{LibUsbMessage}", message);
                break;
            case libusb_log_level.LIBUSB_LOG_LEVEL_INFO:
                logger.LogInformation("{LibUsbMessage}", message);
                break;
            // LibUsbLogLevel.Debug is very verbose and is best mapped to .NET LogLevel.Trace
            case libusb_log_level.LIBUSB_LOG_LEVEL_DEBUG:
                logger.LogTrace("{LibUsbMessage}", message);
                break;
            // Catch the unlikely case that libusb adds another log level in a future version
            default:
                logger.LogError(
                    "Unexpected libusb_log_level '{LibUsbLogLevel}'. {LibUsbMessage}",
                    level,
                    message
                );
                break;
        }
    }

    /// <summary>
    /// Selectively lower the log level to trace for messages that are too verbose.
    /// </summary>
    private static bool LogAsTraceOverride(libusb_log_level level, string message) =>
        IsWindows
        && level is libusb_log_level.LIBUSB_LOG_LEVEL_WARNING
        && (
            // Very frequent during device enumeration on Windows
            message.StartsWith(
                "libusb: warning [init_device] could not open hub ",
                StringComparison.Ordinal
            )
            // Very frequent during device enumeration on Windows
            || message.StartsWith(
                "libusb: warning [winusb_get_device_list] failed to initialize device",
                StringComparison.Ordinal
            )
        );

    /// <summary>
    /// Selectively lower the log level to debug for messages that are too verbose.
    /// </summary>
    private static bool LogAsDebugOverride(libusb_log_level level, string message) =>
        IsWindows
        && (
            // Very frequent on some systems during device enumeration on Windows
            (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_INFO
                && message.StartsWith(
                    "libusb: info [winusb_get_device_list] The following device has no driver:",
                    StringComparison.Ordinal
                )
            )
            // Very frequent on some systems during device enumeration on Windows
            || (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_WARNING
                && message.StartsWith(
                    "libusb: warning [set_composite_interface] failure to read interface number for",
                    StringComparison.Ordinal
                )
            )
            // Frequent when devices are disconnected during enumeration on Windows
            || (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_WARNING
                && message.StartsWith(
                    "libusb: warning [winusb_get_device_list] could not detect installation state of driver for",
                    StringComparison.Ordinal
                )
            )
            // Frequent when devices are disconnected during enumeration on Windows
            || (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_ERROR
                && message.StartsWith("libusb: error [init_device]", StringComparison.Ordinal)
                && (
                    message.EndsWith("has invalid descriptor!", StringComparison.Ordinal)
                    || message.EndsWith("is no longer connected!", StringComparison.Ordinal)
                )
            )
            // Sometimes when a reset is attempted during device disconnect on Windows
            || (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_ERROR
                && message.StartsWith(
                    "libusb: error [winusbx_reset_device]",
                    StringComparison.Ordinal
                )
                && (
                    message.EndsWith(
                        "A device which does not exist was specified.",
                        StringComparison.Ordinal
                    )
                    || message.EndsWith(
                        "The device does not recognize the command.",
                        StringComparison.Ordinal
                    )
                )
            )
            // Sometimes during large data transfers on Windows; even if transfer is successful.
            // TODO: Re-evaluate when https://github.com/libusb/libusb/issues/966 is resolved.
            || (
                level is libusb_log_level.LIBUSB_LOG_LEVEL_ERROR
                && message.Equals(
                    "libusb: error [windows_submit_transfer] program assertion failed - transfer HANDLE is NULL after transfer was submitted",
                    StringComparison.Ordinal
                )
            )
        );
}
