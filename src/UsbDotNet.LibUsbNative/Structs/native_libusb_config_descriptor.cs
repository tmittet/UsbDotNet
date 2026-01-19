using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB configuration descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct native_libusb_config_descriptor
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
    /// Total length of data returned for this configuration.
    /// </summary>
    public ushort wTotalLength;

    /// <summary>
    /// Number of interfaces supported by this configuration.
    /// </summary>
    public byte bNumInterfaces;

    /// <summary>
    /// Identifier value for this configuration.
    /// </summary>
    public byte bConfigurationValue;

    /// <summary>
    /// Index of string descriptor describing this configuration.
    /// </summary>
    public byte iConfiguration;

    /// <summary>
    /// Configuration characteristics.
    /// </summary>
    public byte bmAttributes;

    /// <summary>
    /// Maximum power consumption of the USB device from this bus
    /// in this configuration when the device is fully operation.
    /// </summary>
    public byte MaxPower;

    /// <summary>
    /// A pointer to an array of interfaces (libusb_interface) supported by this configuration.
    /// </summary>
    public nint interfacePtr;

    /// <summary>
    ///  A pointer to a byte array of extra descriptors.
    /// </summary>
    public nint extra;

    /// <summary>
    /// Length of the extra descriptors, in bytes.
    /// </summary>
    public int extra_length;

    /// <summary>
    /// Read the interfaces supported by this configuration and return as a managed list.
    /// </summary>
    public readonly IEnumerable<native_libusb_interface> ReadInterfaces()
    {
        var interfaceByteSize = Marshal.SizeOf<native_libusb_interface>();
        for (var i = 0; i < bNumInterfaces; i++)
        {
            var interfaceHandle = IntPtr.Add(interfacePtr, i * interfaceByteSize);
            yield return Marshal.PtrToStructure<native_libusb_interface>(interfaceHandle);
        }
    }
}
