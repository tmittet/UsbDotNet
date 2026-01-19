using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeDevice : IDisposable
{
    /// <summary>
    /// Open the USB device. Enables you to perform I/O on the device.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the device open operation fails.</exception>
    ISafeDeviceHandle Open();

    /// <summary>
    /// Get the USB device descriptor. NOTE: Since libusb-1.0.16, this function always succeeds.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    libusb_device_descriptor GetDeviceDescriptor();

    /// <summary>
    /// Get the USB configuration descriptor for the currently active configuration.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the get descriptor operation fails.</exception>
    libusb_config_descriptor GetActiveConfigDescriptor();

    /// <summary>
    /// Get a USB configuration descriptor based on its index.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the get descriptor operation fails.</exception>
    libusb_config_descriptor GetConfigDescriptor(byte configIndex);

    /// <summary>
    /// Get the number of the bus that the device is connected to.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    byte GetBusNumber();

    /// <summary>
    /// Get the address of the device on the bus it's connected to.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    byte GetDeviceAddress();

    /// <summary>
    /// Get the number of the port that the device is connected to.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the ISafeDevice is disposed.</exception>
    byte GetPortNumber();

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
