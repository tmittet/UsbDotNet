using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB configuration descriptor.
/// </summary>
public readonly record struct libusb_config_descriptor
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
    /// Total length of data returned for this configuration.
    /// </summary>
    public ushort wTotalLength { get; }

    /// <summary>
    /// Number of interfaces supported by this configuration.
    /// </summary>
    public byte bNumInterfaces { get; }

    /// <summary>
    /// Identifier value for this configuration.
    /// </summary>
    public byte bConfigurationValue { get; }

    /// <summary>
    /// Index of string descriptor describing this configuration.
    /// </summary>
    public byte iConfiguration { get; }

    /// <summary>
    /// Configuration characteristics.
    /// </summary>
    public libusb_config_desc_attributes bmAttributes { get; }

    /// <summary>
    /// Maximum power consumption of the USB device from this bus in this configuration when the device is fully operation.
    /// </summary>
    public byte bMaxPower { get; }

    /// <summary>
    /// Array of interfaces supported by this configuration.
    /// There will always be at least one interface in a valid libusb_config_descriptor.
    /// </summary>
    public IReadOnlyList<libusb_interface> interfaces { get; } = Array.Empty<libusb_interface>();

    /// <summary>
    /// Extra descriptors.
    /// </summary>
    public byte[] extra { get; } = Array.Empty<byte>();

    [JsonConstructor]
    public libusb_config_descriptor(
        byte bLength,
        libusb_descriptor_type bDescriptorType,
        ushort wTotalLength,
        byte bNumInterfaces,
        byte bConfigurationValue,
        byte iConfiguration,
        libusb_config_desc_attributes bmAttributes,
        byte bMaxPower,
        IReadOnlyList<libusb_interface> interfaces,
        byte[] extra
    )
    {
        this.bLength = bLength;
        this.bDescriptorType = bDescriptorType;
        this.wTotalLength = wTotalLength;
        this.bNumInterfaces = bNumInterfaces;
        this.bConfigurationValue = bConfigurationValue;
        this.iConfiguration = iConfiguration;
        this.bmAttributes = bmAttributes;
        this.bMaxPower = bMaxPower;
        this.interfaces = interfaces;
        this.extra = extra;
    }
}
