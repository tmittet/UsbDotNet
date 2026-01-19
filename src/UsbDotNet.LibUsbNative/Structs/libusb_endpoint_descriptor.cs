using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB endpoint descriptor.
/// </summary>
public readonly record struct libusb_endpoint_descriptor
{
    /// <summary>
    /// Size of this descriptor (in bytes).
    /// </summary>
    public byte bLength { get; }

    /// <summary>
    /// Descriptor type.
    /// </summary>
    public libusb_descriptor_type bDescriptorType { get; }

    /// <summary>
    /// The address of the endpoint described by this descriptor.
    /// </summary>
    public libusb_endpoint_address bEndpointAddress { get; }

    /// <summary>
    /// Attributes which apply to the endpoint when it is configured using the bConfigurationValue.
    /// </summary>
    public libusb_endpoint_attributes bmAttributes { get; }

    /// <summary>
    /// Maximum packet size this endpoint is capable of sending/receiving.
    /// </summary>
    public ushort wMaxPacketSize { get; }

    /// <summary>
    /// Interval for polling endpoint for data transfers.
    /// </summary>
    public byte bInterval { get; }

    /// <summary>
    /// For audio devices only: the rate at which synchronization feedback is provided.
    /// </summary>
    public byte bRefresh { get; }

    /// <summary>
    /// For audio devices only: the address if the synch endpoint.
    /// </summary>
    public byte bSynchAddress { get; }

    /// <summary>
    /// Extra descriptors.
    /// </summary>
    public byte[] extra { get; } = Array.Empty<byte>();

    [JsonConstructor]
    public libusb_endpoint_descriptor(
        byte bLength,
        libusb_descriptor_type bDescriptorType,
        libusb_endpoint_address bEndpointAddress,
        libusb_endpoint_attributes bmAttributes,
        ushort wMaxPacketSize,
        byte bInterval,
        byte bRefresh,
        byte bSynchAddress,
        byte[] extra
    )
    {
        this.bLength = bLength;
        this.bDescriptorType = bDescriptorType;
        this.bEndpointAddress = bEndpointAddress;
        this.bmAttributes = bmAttributes;
        this.wMaxPacketSize = wMaxPacketSize;
        this.bInterval = bInterval;
        this.bRefresh = bRefresh;
        this.bSynchAddress = bSynchAddress;
        this.extra = extra;
    }
}
