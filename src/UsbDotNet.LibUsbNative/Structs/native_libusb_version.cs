using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// Native layout for libusb_version (libusb.h)
/// struct libusb_version {
///   const uint16_t major, minor, micro, nano;
///   const char *rc;
///   const char *describe;
/// };
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct native_libusb_version
{
    public ushort major;
    public ushort minor;
    public ushort micro;
    public ushort nano;
    public IntPtr rc;
    public IntPtr describe;
}
