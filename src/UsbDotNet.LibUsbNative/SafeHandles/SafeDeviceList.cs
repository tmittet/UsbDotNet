using System.Collections;
using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeDeviceList : SafeHandle, ISafeDeviceList
{
    private readonly Lazy<IReadOnlyList<SafeDevice>> _lazyDevices;
    private readonly SafeContext _context;

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeDeviceList(SafeContext context, nint listHandle, int count)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        if (listHandle == IntPtr.Zero)
            throw new ArgumentNullException(nameof(listHandle));
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Must be greater than or equal to zero."
            );
        }

        _context = context;
        Count = count;
        handle = listHandle;
        _lazyDevices = new Lazy<IReadOnlyList<SafeDevice>>(() =>
            GetDevices(context, handle, count).ToArray()
        );
    }

    private static IEnumerable<SafeDevice> GetDevices(SafeContext context, nint handle, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var success = false;
            context.DangerousAddRef(ref success);
            yield return success
                ? new SafeDevice(context, Marshal.ReadIntPtr(handle, i * IntPtr.Size))
                : throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException(
                    "Failed to ref SafeHandle."
                );
        }
    }

    public ISafeDevice this[int index] => _lazyDevices.Value[index];

    public int Count { get; }

    public IEnumerator<ISafeDevice> GetEnumerator()
    {
        SafeHelper.ThrowIfClosed(this);
        return _lazyDevices.Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        SafeHelper.ThrowIfClosed(this);
        return _lazyDevices.Value.GetEnumerator();
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        if (_lazyDevices.IsValueCreated)
        {
            foreach (var device in _lazyDevices.Value.Where(d => !d.IsClosed))
            {
                device.Dispose();
            }
        }

        _context.Api.libusb_free_device_list(handle, unref_devices: 1);
        _context.DangerousRelease();
        return true;
    }
}
