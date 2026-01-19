using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeHotplugCallbackHandle : SafeHandle, ISafeCallbackHandle
{
    private readonly SafeContext _context;
    private GCHandle? _gcHandle;

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeHotplugCallbackHandle(SafeContext context)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        _context = context;
    }

    public void Initialize(GCHandle gcHandle, nint callbackHandle)
    {
        if (!gcHandle.IsAllocated)
        {
            throw new ArgumentException("GCHandle not allocated.", nameof(gcHandle));
        }
        if (callbackHandle == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(callbackHandle));
        }
        _gcHandle = gcHandle;
        handle = callbackHandle;
    }

    protected override bool ReleaseHandle()
    {
        _context.Api.libusb_hotplug_deregister_callback(_context.DangerousGetHandle(), handle);
        _gcHandle?.Free();
        _context.DangerousRelease();
        handle = IntPtr.Zero;
        return true;
    }
}
