using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A collection of alternate settings for a particular USB interface.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct native_libusb_interface
{
    /// <summary>
    /// A pointer to an array of interface descriptors.
    /// </summary>
    public nint altsetting;

    /// <summary>
    /// The number of alternate settings that belong to this interface.
    /// </summary>
    public int num_altsetting;

    /// <summary>
    /// Read the altsetting that belong to this interface and return as a managed list.
    /// </summary>
    public readonly IEnumerable<native_libusb_interface_descriptor> ReadAltSettings()
    {
        var interfaceByteSize = Marshal.SizeOf<native_libusb_interface_descriptor>();
        for (var i = 0; i < num_altsetting; i++)
        {
            var interfaceHandle = IntPtr.Add(altsetting, i * interfaceByteSize);
            yield return Marshal.PtrToStructure<native_libusb_interface_descriptor>(
                interfaceHandle
            );
        }
    }
}
