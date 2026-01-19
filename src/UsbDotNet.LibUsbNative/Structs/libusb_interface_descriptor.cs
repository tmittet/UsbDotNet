using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB interface descriptor.
/// </summary>
public readonly record struct libusb_interface_descriptor
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
    /// Number of this interface.
    /// </summary>
    public byte bInterfaceNumber { get; }

    /// <summary>
    /// Value used to select this alternate setting for this interface.
    /// </summary>
    public byte bAlternateSetting { get; }

    /// <summary>
    /// Number of endpoints used by this interface (excluding the control endpoint).
    /// </summary>
    public byte bNumEndpoints { get; }

    /// <summary>
    /// USB-IF class code for this interface.
    /// </summary>
    public libusb_class_code bInterfaceClass { get; }

    /// <summary>
    /// USB-IF subclass code for this interface, qualified by the bInterfaceClass value.
    /// </summary>
    public byte bInterfaceSubClass { get; }

    /// <summary>
    /// USB-IF protocol code for this interface, qualified by the bInterfaceClass and bInterfaceSubClass values.
    /// </summary>
    public byte bInterfaceProtocol { get; }

    /// <summary>
    /// Index of string descriptor describing this interface.
    /// </summary>
    public byte iInterface { get; }

    /// <summary>
    /// Array of endpoint descriptors.
    /// </summary>
    public IReadOnlyList<libusb_endpoint_descriptor> endpoints { get; } =
        Array.Empty<libusb_endpoint_descriptor>();

    /// <summary>
    /// Extra descriptors.
    /// </summary>
    public byte[] extra { get; } = Array.Empty<byte>();

    [JsonConstructor]
    public libusb_interface_descriptor(
        byte bLength,
        libusb_descriptor_type bDescriptorType,
        byte bInterfaceNumber,
        byte bAlternateSetting,
        byte bNumEndpoints,
        libusb_class_code bInterfaceClass,
        byte bInterfaceSubClass,
        byte bInterfaceProtocol,
        byte iInterface,
        IReadOnlyList<libusb_endpoint_descriptor> endpoints,
        byte[] extra
    )
    {
        this.bLength = bLength;
        this.bDescriptorType = bDescriptorType;
        this.bInterfaceNumber = bInterfaceNumber;
        this.bAlternateSetting = bAlternateSetting;
        this.bNumEndpoints = bNumEndpoints;
        this.bInterfaceClass = bInterfaceClass;
        this.bInterfaceSubClass = bInterfaceSubClass;
        this.bInterfaceProtocol = bInterfaceProtocol;
        this.iInterface = iInterface;
        this.endpoints = endpoints;
        this.extra = extra;
    }
}
