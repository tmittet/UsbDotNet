using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Enums;

#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_transfer_status>))]
#endif
public enum libusb_transfer_status : int
{
    /// <summary>
    /// Transfer completed without error (or not started).
    /// This does not indicate that the entire amount of requested data was transferred.
    /// </summary>
    LIBUSB_TRANSFER_COMPLETED = 0,
    LIBUSB_TRANSFER_ERROR,
    LIBUSB_TRANSFER_TIMED_OUT,
    LIBUSB_TRANSFER_CANCELLED,

    /// <summary>
    /// For bulk/interrupt endpoints: halt condition detected (endpoint stalled).
    /// For control endpoints: control request not supported.
    /// </summary>
    LIBUSB_TRANSFER_STALL,

    /// <summary>
    /// Device was disconnected.
    /// </summary>
    LIBUSB_TRANSFER_NO_DEVICE,

    /// <summary>
    /// Device sent more data than requested.
    /// </summary>
    LIBUSB_TRANSFER_OVERFLOW,
}
