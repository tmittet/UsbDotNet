using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.Functions;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void libusb_transfer_cb_fn(nint transfer);
