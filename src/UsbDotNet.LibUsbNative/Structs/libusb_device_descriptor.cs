using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB device descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct libusb_device_descriptor
{
    /// <summary>
    /// Size of this descriptor (in bytes)
    /// </summary>
    public byte bLength { get; }

    /// <summary>
    /// Descriptor type.
    /// </summary>
    public libusb_descriptor_type bDescriptorType { get; }

    /// <summary>
    /// USB specification release number in binary-coded decimal.
    /// </summary>
    public ushort bcdUSB { get; }

    /// <summary>
    /// USB-IF class code for the device.
    /// </summary>
    public libusb_class_code bDeviceClass { get; }

    /// <summary>
    /// USB-IF subclass code for the device, qualified by the bDeviceClass value.
    /// </summary>
    public byte bDeviceSubClass { get; }

    /// <summary>
    /// USB-IF protocol code for the device, qualified by the bDeviceClass and bDeviceSubClass values.
    /// </summary>
    public byte bDeviceProtocol { get; }

    /// <summary>
    /// Maximum packet size for endpoint 0.
    /// </summary>
    public byte bMaxPacketSize0 { get; }

    /// <summary>
    /// USB-IF vendor ID.
    /// </summary>
    public ushort idVendor { get; }

    /// <summary>
    /// USB-IF product ID.
    /// </summary>
    public ushort idProduct { get; }

    /// <summary>
    /// Device release number in binary-coded decimal.
    /// </summary>
    public ushort bcdDevice { get; }

    /// <summary>
    /// Index of string descriptor describing manufacturer.
    /// </summary>
    public byte iManufacturer { get; }

    /// <summary>
    /// Index of string descriptor describing product.
    /// </summary>
    public byte iProduct { get; }

    /// <summary>
    /// Index of string descriptor containing device serial number.
    /// </summary>
    public byte iSerialNumber { get; }

    /// <summary>
    /// Number of possible configurations.
    /// </summary>
    public byte bNumConfigurations { get; }

    [JsonConstructor]
    public libusb_device_descriptor(
        byte bLength,
        libusb_descriptor_type bDescriptorType,
        ushort bcdUSB,
        libusb_class_code bDeviceClass,
        byte bDeviceSubClass,
        byte bDeviceProtocol,
        byte bMaxPacketSize0,
        ushort idVendor,
        ushort idProduct,
        ushort bcdDevice,
        byte iManufacturer,
        byte iProduct,
        byte iSerialNumber,
        byte bNumConfigurations
    )
    {
        this.bLength = bLength;
        this.bDescriptorType = bDescriptorType;
        this.bcdUSB = bcdUSB;
        this.bDeviceClass = bDeviceClass;
        this.bDeviceSubClass = bDeviceSubClass;
        this.bDeviceProtocol = bDeviceProtocol;
        this.bMaxPacketSize0 = bMaxPacketSize0;
        this.idVendor = idVendor;
        this.idProduct = idProduct;
        this.bcdDevice = bcdDevice;
        this.iManufacturer = iManufacturer;
        this.iProduct = iProduct;
        this.iSerialNumber = iSerialNumber;
        this.bNumConfigurations = bNumConfigurations;
    }
}
