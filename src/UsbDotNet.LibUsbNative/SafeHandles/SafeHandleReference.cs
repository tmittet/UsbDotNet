using System.Runtime.InteropServices;

namespace UsbDotNet.LibUsbNative.SafeHandles;

/// <summary>
/// A reference held on a safe handle, released on dispose.
/// </summary>
internal readonly ref struct SafeHandleReference
{
    private readonly SafeHandle? _safeHandle;

    /// <exception cref="ObjectDisposedException">Thrown when the safe handle is closed.</exception>
    internal SafeHandleReference(SafeHandle safeHandle)
    {
        var success = false;
        // Throws ObjectDisposedException when the safe handle is closed
        safeHandle.DangerousAddRef(ref success);
        _safeHandle = success ? safeHandle : null;
    }

    public void Dispose() => _safeHandle?.DangerousRelease();
}
