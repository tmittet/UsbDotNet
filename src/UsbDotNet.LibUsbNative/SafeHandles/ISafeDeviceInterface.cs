namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeDeviceInterface : IDisposable
{
    /// <summary>
    /// Get the interface number.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDeviceInterface is disposed.</exception>
    int GetInterfaceNumber();

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
