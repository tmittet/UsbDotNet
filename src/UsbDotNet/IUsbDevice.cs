using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Transfer;

namespace UsbDotNet;

/// <summary>
/// Represents a USB device. Provides access to device descriptors
/// and methods to interact with the device and its interfaces.
/// </summary>
public interface IUsbDevice : IDisposable
{
    /// <summary>
    /// A device descriptor that includes device class, vendor ID, product ID, bus address and more.
    /// It is safe to read/inspect 'Descriptor' information after the UsbDevice has been disposed.
    /// </summary>
    IUsbDeviceDescriptor Descriptor { get; }

    /// <summary>
    /// A device config descriptor that includes information about device interfaces and endpoints.
    /// It is safe to read/inspect 'ConfigDescriptor' info after the UsbDevice has been disposed.
    /// </summary>
    IUsbConfigDescriptor ConfigDescriptor { get; init; }

    /// <summary>
    /// Reads the manufacturer from the device if required; otherwise a cached value is returned.
    /// To read uncached call device.ReadStringDescriptor(device.Descriptor.ManufacturerIndex).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    string GetManufacturer();

    /// <summary>
    /// Reads the product name from the device if required; otherwise a cached value is returned.
    /// To read uncached, call device.ReadStringDescriptor(device.Descriptor.ProductIndex).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    string GetProduct();

    /// <summary>
    /// Reads the serial number from the device if required; otherwise a cached value is returned.
    /// To read uncached, call device.ReadStringDescriptor(device.Descriptor.SerialNumberIndex).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    string GetSerialNumber();

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the device.
    /// NOTE: On some devices it may fail even for basic fields like serial number (at index 0).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    string ReadStringDescriptor(byte descriptorIndex);

    /// <summary>
    /// Send a ControlRead request. Data is read Device -> Host.
    /// </summary>
    /// <param name="destination">A destination span for read bytes</param>
    /// <param name="bytesRead">The number of bytes read</param>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="type">The control request type; standard, class or vendor</param>
    /// <param name="request">The USB standard spec, class spec or vendor defined request</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the destination buffer is too large.</exception>
    /// <returns>
    /// Success = The read operation completed successfully.<br />
    /// IO = The read operation failed.<br />
    /// InvalidParameter = Transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The read operation timed out.<br />
    /// Overflow = The device sent more data than expected.<br />
    /// Interrupted = The read operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    UsbResult ControlRead(
        Span<byte> destination,
        out ushort bytesRead,
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte request,
        ushort value,
        ushort index,
        int timeout = Timeout.Infinite
    );

    /// <summary>
    /// Send a ControlWrite request. Data is written Host -> Device.
    /// </summary>
    /// <param name="source">The payload to send to the device (max. 65_535 bytes)</param>
    /// <param name="bytesWritten">The actual number of bytes written to the device</param>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="type">The control request type; standard, class or vendor</param>
    /// <param name="request">The USB standard spec, class spec or vendor defined request</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="timeout">Timeout before giving up due to no response being received</param>
    /// <exception cref="ArgumentException">Thrown when the source payload is too large.</exception>
    /// <returns>
    /// Success = The write operation completed successfully.<br />
    /// IO = The write operation failed.<br />
    /// InvalidParameter = Transfer size is larger than OS or hardware can support.<br />
    /// NoDevice = The device has been disconnected.<br />
    /// ResourceBusy = Halt condition detected (endpoint stalled) or control request not supported.<br />
    /// Timeout = The write operation timed out.<br />
    /// Overflow = The host sent more data than expected.<br />
    /// Interrupted = The write operation was canceled.<br />
    /// NotSupported = The transfer flags are not supported by the operating system.<br />
    /// </returns>
    UsbResult ControlWrite(
        ReadOnlySpan<byte> source,
        out int bytesWritten,
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte request,
        ushort value,
        ushort index,
        int timeout = Timeout.Infinite
    );

    /// <summary>
    /// Claim a USB interface. The interface will be auto-released when the device is disposed.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the USB interface is already claimed.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when the USB interface claim operation fails.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the UsbDevice is disposed.
    /// </exception>
    IUsbInterface ClaimInterface(IUsbInterfaceDescriptor descriptor);

    /// <summary>
    /// WARNING: Use very carefully! Performs a USB port reset to reconnect/reinitialize the device.
    /// The system will attempt to restore the previous configuration and alternate settings after
    /// the reset has completed. If the reset fails, the descriptors change, or the previous state
    /// cannot be restored, the device will appear to be disconnected and reconnected.
    /// </summary>
    void Reset();
}
