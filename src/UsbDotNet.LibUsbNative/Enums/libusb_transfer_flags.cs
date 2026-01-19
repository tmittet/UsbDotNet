#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

[Flags]
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_transfer_flags>))]
#endif
public enum libusb_transfer_flags : byte
{
    NONE = 0,

    /// <summary>
    /// Report short frames as errors.
    /// </summary>
    LIBUSB_TRANSFER_SHORT_NOT_OK = 1 << 0,

    /// <summary>
    /// Automatically free() transfer buffer during libusb_free_transfer().
    /// Note that buffers allocated with libusb_dev_mem_alloc() should not be attempted freed in
    /// this way, since free() is not an appropriate way to release such memory.
    /// </summary>
    LIBUSB_TRANSFER_FREE_BUFFER = 1 << 1,

    /// <summary>
    /// Automatically call libusb_free_transfer() after callback returns. If this flag is set, it is
    /// illegal to call libusb_free_transfer() from your transfer callback, as this will result in a
    /// double-free when this flag is acted upon.
    /// </summary>
    LIBUSB_TRANSFER_FREE_TRANSFER = 1 << 2,

    /// <summary>
    /// Terminate transfers that are a multiple of the endpoint's wMaxPacketSize with an extra zero
    /// length packet. This is useful when a device protocol mandates that each logical request is
    /// terminated by an incomplete packet (i.e. the logical requests are not separated by other
    /// means). This flag only affects host-to-device transfers to bulk and interrupt endpoints. In
    /// other situations, it is ignored. This flag only affects transfers with a length that is a
    /// multiple of the endpoint's wMaxPacketSize. On transfers of other lengths, this flag has no
    /// effect. Therefore, if you are working with a device that needs a ZLP whenever the end of the
    /// logical request falls on a packet boundary, then it is sensible to set this flag on every
    /// transfer (you do not have to worry about only setting it on transfers that end on the
    /// boundary). This flag is currently only supported on Linux. On other systems,
    /// libusb_submit_transfer() will return LIBUSB_ERROR_NOT_SUPPORTED for every transfer where
    /// this flag is set.
    /// </summary>
    LIBUSB_TRANSFER_ADD_ZERO_PACKET = 1 << 3,
}
