using Microsoft.Extensions.Logging;
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
    /// The logger factory used by this device instance.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// A device descriptor that includes device class, vendor ID, product ID, bus address and more.
    /// It is safe to read/inspect 'Descriptor' information after the UsbDevice has been disposed.
    /// </summary>
    IUsbDeviceDescriptor Descriptor { get; }

    /// <summary>
    /// A device config descriptor that includes information about device interfaces and endpoints.
    /// It is safe to read/inspect 'ConfigDescriptor' info after the UsbDevice has been disposed.
    /// </summary>
    IUsbConfigDescriptor ConfigDescriptor { get; }

    /// <summary>
    /// Reads the manufacturer from the device if required; otherwise a cached value is returned.
    /// To read uncached call device.ReadStringDescriptor(device.Descriptor.ManufacturerIndex).
    /// </summary>
    /// <exception cref="UsbException">Thrown when the descriptor read operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    /// <returns>
    /// The manufacturer, or an empty string when the device has no manufacturer string.
    /// </returns>
    string GetManufacturer();

    /// <summary>
    /// Reads the product name from the device if required; otherwise a cached value is returned.
    /// To read uncached, call device.ReadStringDescriptor(device.Descriptor.ProductIndex).
    /// </summary>
    /// <exception cref="UsbException">Thrown when the descriptor read operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    /// <returns>
    /// The product name, or an empty string when the device has no product name string.
    /// </returns>
    string GetProduct();

    /// <summary>
    /// Reads the serial number from the device if required; otherwise a cached value is returned.
    /// To read uncached, call device.ReadStringDescriptor(device.Descriptor.SerialNumberIndex).
    /// </summary>
    /// <exception cref="UsbException">Thrown when the descriptor read operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    /// <returns>
    /// The serial number, or an empty string when the device has no serial number string.
    /// </returns>
    string GetSerialNumber();

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the device.
    /// The string is returned as sent by the device; characters that are not ASCII are kept.
    /// <para>
    /// NOTE: On some devices it may fail even for basic fields like the serial number.
    /// </para>
    /// </summary>
    /// <param name="descriptorIndex">
    /// The index of the string descriptor, typically from a device, configuration or interface
    /// descriptor. An index of zero means that the device does not have the string.
    /// </param>
    /// <exception cref="UsbException">Thrown when the descriptor read operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    /// <returns>
    /// The string, or an empty string when <paramref name="descriptorIndex"/> is zero; no request
    /// is then sent to the device.
    /// </returns>
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
    /// <param name="timeout">
    /// Timeout in milliseconds before giving up due to no response being received; greater than
    /// 0, or -1 (<see cref="Timeout.Infinite"/>) for no timeout.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the destination buffer is too large, or when timeout is 0 or less than -1.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The read operation completed successfully.</item>
    /// <item>IO = The read operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>
    /// PipeError = Halt condition detected (endpoint stalled) or control request not supported.
    /// </item>
    /// <item>Timeout = The read operation timed out.</item>
    /// <item>Overflow = The device sent more data than expected.</item>
    /// <item>Interrupted = The read operation was canceled, or the device is disposed.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
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
    /// <param name="timeout">
    /// Timeout in milliseconds before giving up due to no response being received; greater than
    /// 0, or -1 (<see cref="Timeout.Infinite"/>) for no timeout.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the source payload is too large, or when timeout is 0 or less than -1.
    /// </exception>
    /// <returns>
    /// <list>
    /// <item>Success = The write operation completed successfully.</item>
    /// <item>IO = The write operation failed.</item>
    /// <item>InvalidParameter = Transfer size is larger than OS or hardware can support.</item>
    /// <item>NoDevice = The device has been disconnected.</item>
    /// <item>
    /// PipeError = Halt condition detected (endpoint stalled) or control request not supported.
    /// </item>
    /// <item>Timeout = The write operation timed out.</item>
    /// <item>Overflow = The host sent more data than expected.</item>
    /// <item>Interrupted = The write operation was canceled, or the device is disposed.</item>
    /// <item>NotSupported = The transfer flags are not supported by the operating system.</item>
    /// </list>
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
    /// <para>
    /// The alternate setting of <paramref name="descriptor"/> is selected on the device. A
    /// SET_INTERFACE request is sent to the device when it is not the default alternate setting
    /// (0), and also for the default when another alternate setting of the interface has been
    /// selected through this device earlier. The alternate setting an interface is left on
    /// when it is released depends on the operating system.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="descriptor"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the USB interface is already claimed, with any alternate setting.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when the USB interface claim operation fails, or when the alternate setting cannot
    /// be selected; the interface is then not claimed.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the UsbDevice is disposed.
    /// </exception>
    IUsbInterface ClaimInterface(IUsbInterfaceDescriptor descriptor);

    /// <summary>
    /// WARNING: Use very carefully! Performs a USB port reset to reconnect/reinitialize the device.
    /// <para>
    /// The system will attempt to restore the previous configuration and alternate settings after
    /// the reset has completed. If the reset fails, the descriptors change, or the previous state
    /// cannot be restored, the device will appear to be disconnected and reconnected.
    /// </para>
    /// </summary>
    /// <exception cref="UsbException">Thrown when the reset operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the UsbDevice is disposed.</exception>
    void Reset();
}
