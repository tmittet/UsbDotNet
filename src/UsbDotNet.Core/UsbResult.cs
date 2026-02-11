namespace UsbDotNet.Core;

public enum UsbResult : int
{
    Success = 0,

    //
    // Errors that originate from libusb
    //

    /// <summary>
    /// Input/output error (LIBUSB_ERROR_IO)
    /// </summary>
    IoError = -1,

    /// <summary>
    /// Invalid parameter (LIBUSB_ERROR_INVALID_PARAM)
    /// </summary>
    InvalidParameter = -2,

    /// <summary>
    /// Access denied or insufficient permissions (LIBUSB_ERROR_ACCESS)
    /// </summary>
    AccessDenied = -3,

    /// <summary>
    /// No such device or it may have been disconnected (LIBUSB_ERROR_NO_DEVICE)
    /// </summary>
    NoDevice = -4,

    /// <summary>
    /// Entity not found (LIBUSB_ERROR_NOT_FOUND)
    /// </summary>
    NotFound = -5,

    /// <summary>
    /// Resource busy (LIBUSB_ERROR_BUSY)
    /// </summary>
    ResourceBusy = -6,

    /// <summary>
    /// Operation timed out (LIBUSB_ERROR_TIMEOUT)
    /// </summary>
    Timeout = -7,

    /// <summary>
    /// Overflow (LIBUSB_ERROR_OVERFLOW)
    /// </summary>
    Overflow = -8,

    /// <summary>
    /// Pipe error (LIBUSB_ERROR_PIPE)
    /// </summary>
    PipeError = -9,

    /// <summary>
    /// System call interrupted, perhaps due to signal (LIBUSB_ERROR_INTERRUPTED)
    /// </summary>
    Interrupted = -10,

    /// <summary>
    /// Insufficient memory (LIBUSB_ERROR_NO_MEM)
    /// </summary>
    InsufficientMemory = -11,

    /// <summary>
    /// Operation not supported or unimplemented on this platform (LIBUSB_ERROR_NOT_SUPPORTED)
    /// </summary>
    NotSupported = -12,

    /// <summary>
    /// Other error (LIBUSB_ERROR_OTHER)
    /// </summary>
    OtherError = -99,

    /// <summary>
    /// Unknown error (catch-all for undefined libusb errors)
    /// </summary>
    UnknownError = 0x7FFFFFFF, // int.MaxValue
}
