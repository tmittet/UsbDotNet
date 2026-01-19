#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

/// <summary>
/// Synchronization type for isochronous endpoints.
/// Values for bits 2:3 of the bmAttributes field in libusb_endpoint_descriptor.
/// </summary>
#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_iso_sync_type>))]
#endif
public enum libusb_iso_sync_type : byte
{
    /// <summary>No synchronization.</summary>
    LIBUSB_ISO_SYNC_TYPE_NONE = 0,

    /// <summary>Asynchronous.</summary>
    LIBUSB_ISO_SYNC_TYPE_ASYNC = 1,

    /// <summary>Adaptive.</summary>
    LIBUSB_ISO_SYNC_TYPE_ADAPTIVE = 2,

    /// <summary>Synchronous.</summary>
    LIBUSB_ISO_SYNC_TYPE_SYNC = 3,
}
