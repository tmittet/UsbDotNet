#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_log_level>))]
#endif
public enum libusb_log_level : byte
{
    /// <summary>
    /// No messages ever emitted by the library (default)
    /// </summary>
    LIBUSB_LOG_LEVEL_NONE = 0,

    /// <summary>
    /// Error messages are emitted
    /// </summary>
    LIBUSB_LOG_LEVEL_ERROR = 1,

    /// <summary>
    /// Warning and error messages are emitted
    /// </summary>
    LIBUSB_LOG_LEVEL_WARNING = 2,

    /// <summary>
    /// Informational, warning and error messages are emitted
    /// </summary>
    LIBUSB_LOG_LEVEL_INFO = 3,

    /// <summary>
    /// All messages are emitted
    /// </summary>
    LIBUSB_LOG_LEVEL_DEBUG = 4,
}
