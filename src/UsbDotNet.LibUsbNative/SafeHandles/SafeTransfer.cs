using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeTransfer : SafeHandle, ISafeTransfer
{
    private readonly SafeContext _context;

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeTransfer(SafeContext context, nint transferHandle)
        : base(IntPtr.Zero, true)
    {
        if (transferHandle == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(transferHandle));
        }
        _context = context;
        handle = transferHandle;
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        _context.Api.libusb_free_transfer(handle);
        return true;
    }

    public nint GetBufferPtr()
    {
        SafeHelper.ThrowIfClosed(this);
        return DangerousGetHandle();
    }

    public libusb_error Submit()
    {
        SafeHelper.ThrowIfClosed(this);
        return _context.Api.libusb_submit_transfer(handle);
    }

    public libusb_error Cancel()
    {
        SafeHelper.ThrowIfClosed(this);
        return _context.Api.libusb_cancel_transfer(handle);
    }
}
