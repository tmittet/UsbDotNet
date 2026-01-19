using System.Diagnostics;
using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative;

/// <summary>
/// Singleton-style access to libusb API. Swap in tests if needed.
/// </summary>
public class LibUsb : ILibUsb
{
    private readonly ILibUsbApi _api;

    public LibUsb(ILibUsbApi? api = default)
    {
        _api = api ?? new PInvokeLibUsbApi();
    }

    public ISafeContext CreateContext()
    {
        return new SafeContext(_api);
    }

    public bool HasCapability(libusb_capability capability) =>
        _api.libusb_has_capability(capability) != 0;

    /// <summary>
    /// Returns the full libusb version structure.
    /// </summary>
    public libusb_version GetVersion()
    {
        var p = _api.libusb_get_version();
        if (p == IntPtr.Zero)
        {
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException(
                $"LibUsbApi '{nameof(_api.libusb_get_version)}' returned a null pointer."
            );
        }

        var native = Marshal.PtrToStructure<native_libusb_version>(p);

        static string PtrToString(IntPtr sp) =>
            sp == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringAnsi(sp) ?? string.Empty);

        return new libusb_version(
            native.major,
            native.minor,
            native.micro,
            native.nano,
            PtrToString(native.rc),
            PtrToString(native.describe)
        );
    }

    public string StrError(libusb_error usbError)
    {
        var ptr = _api.libusb_strerror(usbError);
        Debug.Assert(ptr != IntPtr.Zero, "libusb_strerror returned null pointer");

        var detail = Marshal.PtrToStringAnsi(ptr);
        return detail is null
            ? $"LibUsb error code {usbError}."
            : $"LibUsb error code {usbError}: {detail}.";
    }
}
