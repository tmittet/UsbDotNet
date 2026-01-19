using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Functions;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void libusb_log_cb(nint ctx, libusb_log_level level, string str);
