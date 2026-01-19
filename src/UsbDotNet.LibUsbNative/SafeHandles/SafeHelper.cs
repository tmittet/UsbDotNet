using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal static class SafeHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfClosed(SafeHandle safeHandle, string? objectName = "SafeHandle")
    {
        if (safeHandle.IsClosed || safeHandle.IsInvalid)
        {
            throw new ObjectDisposedException(objectName);
        }
    }
}
