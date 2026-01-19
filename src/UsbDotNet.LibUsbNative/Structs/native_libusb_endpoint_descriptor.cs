using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB endpoint descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct native_libusb_endpoint_descriptor
{
    /// <summary>
    /// Size of this descriptor (in bytes).
    /// </summary>
    public byte bLength;

    /// <summary>
    /// Descriptor type.
    /// </summary>
    public byte bDescriptorType;

    /// <summary>
    /// The address of the endpoint described by this descriptor.
    /// </summary>
    public byte bEndpointAddress;

    /// <summary>
    /// Attributes which apply to the endpoint when it is configured using the bConfigurationValue.
    /// </summary>
    public byte bmAttributes;

    /// <summary>
    /// Maximum packet size this endpoint is capable of sending/receiving.
    /// </summary>
    public ushort wMaxPacketSize;

    /// <summary>
    /// Interval for polling endpoint for data transfers.
    /// </summary>
    public byte bInterval;

    /// <summary>
    /// For audio devices only: the rate at which synchronization feedback is provided.
    /// </summary>
    public byte bRefresh;

    /// <summary>
    /// For audio devices only: the address if the synch endpoint.
    /// </summary>
    public byte bSynchAddress;

    /// <summary>
    /// Extra descriptors.
    /// </summary>
    public nint extra;

    /// <summary>
    /// Length of the extra descriptors, in bytes.
    /// </summary>
    public int extra_length;
}
