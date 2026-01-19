using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// The generic USB transfer structure. The user populates this structure and then submits it in
/// order to request a transfer. After the transfer has completed, the library populates the
/// transfer with the results and passes it back to the user.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct libusb_transfer
{
    public static libusb_transfer Create(
        ISafeDeviceHandle dev_handle,
        byte endpoint,
        GCHandle buffer,
        int length,
        libusb_endpoint_transfer_type type,
        uint timeout,
        libusb_transfer_cb_fn callback
    )
    {
        return new libusb_transfer
        {
            dev_handle = dev_handle.DangerousGetHandle(),
            endpoint = endpoint,
            type = type,
            timeout = timeout,
            length = length,
            callback = callback,
            buffer = buffer.AddrOfPinnedObject(),
        };
    }

    /// <summary>
    /// Handle of the device that this transfer will be submitted to.
    /// </summary>
    public nint dev_handle { get; private init; }

    /// <summary>
    /// A bitwise OR combination of libusb_transfer_flags.
    /// </summary>
    public libusb_transfer_flags flags { get; private init; }

    /// <summary>
    /// Address of the endpoint where this transfer will be sent.
    /// </summary>
    public byte endpoint { get; private init; }

    /// <summary>
    /// Type of the transfer from libusb_transfer_type.
    /// </summary>
    public libusb_endpoint_transfer_type type { get; private init; }

    /// <summary>
    /// Timeout for this transfer in milliseconds.
    /// </summary>
    public uint timeout { get; private init; }

    /// <summary>
    /// The status of the transfer.
    /// </summary>
    public libusb_transfer_status status { get; private init; }

    /// <summary>
    /// Length of the data buffer.
    /// </summary>
    public int length { get; private init; }

    /// <summary>
    /// Actual length of data that was transferred.
    /// </summary>
    public int actual_length { get; private init; }

    /// <summary>
    /// Callback function.
    /// </summary>
    public libusb_transfer_cb_fn callback { get; private init; }

    /// <summary>
    /// User context data.
    /// </summary>
    public nint user_data { get; private init; }

    /// <summary>
    /// Data buffer.
    /// </summary>
    public nint buffer { get; private init; }

    /// <summary>
    /// Number of isochronous packets.
    /// </summary>
    public int num_iso_packets { get; private init; }

    // TODO: iso_packet_desc: Isochronous packet descriptors, for isochronous transfers only.

    [JsonConstructor]
    public libusb_transfer(
        nint dev_handle,
        libusb_transfer_flags flags,
        byte endpoint,
        libusb_endpoint_transfer_type type,
        uint timeout,
        libusb_transfer_status status,
        int length,
        int actual_length,
        libusb_transfer_cb_fn callback,
        nint user_data,
        nint buffer,
        int num_iso_packets
    )
    {
        this.dev_handle = dev_handle;
        this.flags = flags;
        this.endpoint = endpoint;
        this.type = type;
        this.timeout = timeout;
        this.status = status;
        this.length = length;
        this.actual_length = actual_length;
        this.callback = callback;
        this.user_data = user_data;
        this.buffer = buffer;
        this.num_iso_packets = num_iso_packets;
    }
}
