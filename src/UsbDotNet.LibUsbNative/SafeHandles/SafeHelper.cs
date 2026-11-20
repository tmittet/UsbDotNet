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

    /// <summary>
    /// Hold a reference on the safe handle for the duration of a native call; dispose the result
    /// to release it. A check with <see cref="ThrowIfClosed"/> alone does not stop another thread
    /// from disposing the safe handle, and releasing the native resource, before or during the
    /// native call. With a reference held, the native resource is released when the last
    /// reference is released.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the safe handle is closed.</exception>
    public static SafeHandleReference AcquireReference(
        SafeHandle safeHandle,
        string? objectName = "SafeHandle"
    )
    {
        ThrowIfClosed(safeHandle, objectName);
        return new SafeHandleReference(safeHandle);
    }
}
