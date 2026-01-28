using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
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

    /// <summary>
    /// Attempt to log a message using the registered log handler; if there is one.
    /// </summary>
    internal void Log(libusb_log_level level, string message) => _deviceHandle.Log(level, message);

    protected override bool ReleaseHandle()
    {
        var result = _deviceHandle.Api.libusb_release_interface(
            _deviceHandle.DangerousGetHandle(),
            _interfaceNumber
        );
        // Throwing exceptions in ReleaseHandle is not allowed. Log error and return false.
        if (result != libusb_error.LIBUSB_SUCCESS)
        {
            var logLevel =
                result == libusb_error.LIBUSB_ERROR_NO_DEVICE
                    ? libusb_log_level.LIBUSB_LOG_LEVEL_INFO
                    : libusb_log_level.LIBUSB_LOG_LEVEL_WARNING;
            Log(
                logLevel,
                $"LibUsbApi '{nameof(_deviceHandle.Api.libusb_release_interface)}' failed; "
                    + $"interface {_interfaceNumber}. {result}: {result.GetMessage()}."
            );
            return false;
        }
        return true;
    }
}
