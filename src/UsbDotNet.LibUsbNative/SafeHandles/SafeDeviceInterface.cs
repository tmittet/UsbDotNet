using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeDeviceInterface : SafeHandle, ISafeDeviceInterface
{
    private readonly SafeDeviceHandle _deviceHandle;
    private readonly byte _interfaceNumber;

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeDeviceInterface(SafeDeviceHandle deviceHandle, byte interfaceNumber)
        : base(IntPtr.Zero, true)
    {
        _deviceHandle = deviceHandle;
        _interfaceNumber = interfaceNumber;
        // The handle of SafeDeviceInterface is not used. A value other than zero set here
        // to make IsInvalid return the correct bool value once ReleaseHandle is done.
        handle = new IntPtr(-1);
    }

    public int GetInterfaceNumber()
    {
        SafeHelper.ThrowIfClosed(this);
        return _interfaceNumber;
    }

    protected override bool ReleaseHandle()
    {
        var result = _deviceHandle.Api.libusb_release_interface(
            _deviceHandle.DangerousGetHandle(),
            _interfaceNumber
        );
        result.ThrowLibUsbExceptionForApi(
            nameof(_deviceHandle.Api.libusb_release_interface),
            $"Interface {_interfaceNumber}."
        );
        return true;
    }
}
