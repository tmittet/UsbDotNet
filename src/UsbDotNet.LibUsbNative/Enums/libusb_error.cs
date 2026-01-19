#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// libusb return codes. Most libusb functions return 0 on success or a negative error code.
/// </summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_error>))]
#endif
public enum libusb_error : int
{
    LIBUSB_SUCCESS = 0,

    /// <summary>Input/output error.</summary>
    LIBUSB_ERROR_IO = -1,

    /// <summary>Invalid parameter.</summary>
    LIBUSB_ERROR_INVALID_PARAM = -2,

    /// <summary>Access denied (insufficient permissions).</summary>
    LIBUSB_ERROR_ACCESS = -3,

    /// <summary>No such device (it may have been disconnected).</summary>
    LIBUSB_ERROR_NO_DEVICE = -4,

    /// <summary>Entity not found.</summary>
    LIBUSB_ERROR_NOT_FOUND = -5,

    /// <summary>Resource busy.</summary>
    LIBUSB_ERROR_BUSY = -6,

    /// <summary>Operation timed out.</summary>
    LIBUSB_ERROR_TIMEOUT = -7,

    /// <summary>Overflow (device sent more data than requested).</summary>
    LIBUSB_ERROR_OVERFLOW = -8,

    /// <summary>Pipe error (stall).</summary>
    LIBUSB_ERROR_PIPE = -9,

    /// <summary>System call was interrupted (retry might succeed).</summary>
    LIBUSB_ERROR_INTERRUPTED = -10,

    /// <summary>Insufficient memory.</summary>
    LIBUSB_ERROR_NO_MEM = -11,

    /// <summary>Operation not supported or unimplemented on this platform.</summary>
    LIBUSB_ERROR_NOT_SUPPORTED = -12,

    // -99 is reserved as a catch-all
    LIBUSB_ERROR_OTHER = -99,
}
