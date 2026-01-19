using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Functions;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate libusb_hotplug_return libusb_hotplug_callback_fn(
    nint ctx,
    nint device,
    libusb_hotplug_event event_type,
    nint user_data
);
