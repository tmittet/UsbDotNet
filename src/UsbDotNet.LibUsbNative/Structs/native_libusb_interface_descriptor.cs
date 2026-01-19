using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A structure representing the standard USB interface descriptor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct native_libusb_interface_descriptor
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
    /// Number of this interface.
    /// </summary>
    public byte bInterfaceNumber;

    /// <summary>
    /// Value used to select this alternate setting for this interface.
    /// </summary>
    public byte bAlternateSetting;

    /// <summary>
    /// Number of endpoints used by this interface (excluding the control endpoint).
    /// </summary>
    public byte bNumEndpoints;

    /// <summary>
    /// USB-IF class code for this interface.
    /// </summary>
    public byte bInterfaceClass;

    /// <summary>
    /// USB-IF subclass code for this interface, qualified by the bInterfaceClass value.
    /// </summary>
    public byte bInterfaceSubClass;

    /// <summary>
    /// USB-IF protocol code for this interface, qualified by the bInterfaceClass and bInterfaceSubClass values
    /// </summary>
    public byte bInterfaceProtocol;

    /// <summary>
    /// Index of string descriptor describing this interface.
    /// </summary>
    public byte iInterface;

    /// <summary>
    /// A pointer to an array of endpoint descriptors.(libusb_endpoint_descriptor).
    /// </summary>
    public nint endpoint;

    /// <summary>
    /// Extra descriptors.
    /// </summary>
    public nint extra;

    /// <summary>
    /// Length of the extra descriptors, in bytes.
    /// </summary>
    public int extra_length;

    /// <summary>
    /// Read the endpoint descriptors and return as a managed list.
    /// </summary>
    public readonly IEnumerable<native_libusb_endpoint_descriptor> ReadEndpoints()
    {
        var interfaceByteSize = Marshal.SizeOf<native_libusb_endpoint_descriptor>();
        for (var i = 0; i < bNumEndpoints; i++)
        {
            var interfaceHandle = IntPtr.Add(endpoint, i * interfaceByteSize);
            yield return Marshal.PtrToStructure<native_libusb_endpoint_descriptor>(interfaceHandle);
        }
    }
}
