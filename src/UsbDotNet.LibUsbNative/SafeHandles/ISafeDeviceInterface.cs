namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeDeviceInterface : IDisposable
{
    /// <summary>
    /// Get the interface number.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDeviceInterface is disposed.</exception>
    int GetInterfaceNumber();

    /// <summary>
    /// Activate an alternate setting for the claimed interface. This is a blocking function; a
    /// SET_INTERFACE request is sent to the device. Releasing the interface resets it to the
    /// first alternate setting.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDeviceInterface is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the set alternate setting operation fails.</exception>
    void SetAlternateSetting(byte alternateSetting);

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
