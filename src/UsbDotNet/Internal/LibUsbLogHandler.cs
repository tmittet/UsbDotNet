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
        // Selectively lower the log level for INFO, WARN and ERROR messages that are too verbose
        if (level is not libusb_log_level.LIBUSB_LOG_LEVEL_DEBUG)
        {
            if (LogTraceOverride(message))
            {
                logger.LogTrace("{LibUsbMessage}", message);
                return;
            }
            if (LogDebugOverride(message))
            {
                logger.LogDebug("{LibUsbMessage}", message);
                return;
            }
        }
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
    private static bool LogTraceOverride(string message) =>
        IsWindows
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
    private static bool LogDebugOverride(string message) =>
        IsWindows
        && (
            // Frequent when devices are disconnected during enumeration on Windows
            (
                message.StartsWith("libusb: error [init_device]", StringComparison.Ordinal)
                && (
                    message.EndsWith("has invalid descriptor!", StringComparison.Ordinal)
                    || message.EndsWith("is no longer connected!", StringComparison.Ordinal)
                )
            )
            // Frequent when devices are disconnected during enumeration on Windows
            || message.StartsWith(
                "libusb: warning [winusb_get_device_list] could not detect installation state of driver for",
                StringComparison.Ordinal
            )
            // Sometimes when a reset is attempted during device disconnect on Windows
            || (
                message.StartsWith("libusb: error [winusbx_reset_device]", StringComparison.Ordinal)
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
        );
}
