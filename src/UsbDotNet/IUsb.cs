using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet;

public interface IUsb : IDisposable
{
    /// <summary>
    /// Initializes the USB library (libusb), attaches a log callback and starts the
    /// background thread that handles USB events and drives async transfers.
    /// </summary>
    /// <param name="logLevel">The desired USB library (libusb) log level.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    void Initialize(LogLevel logLevel = LogLevel.Warning);

    /// <summary>
    /// Hotplug events are supported on macOS, Linux and Windows.
    /// https://libusb.sourceforge.io/api-1.0/libusb_hotplug.html
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    bool RegisterHotplug(
        UsbClass? deviceClass = null,
        ushort? vendorId = null,
        ushort? productId = null
    );

    /// <summary>
    /// Returns a list of device descriptors for connected USB devices.
    /// It does not involve any requests being sent to the devices.
    /// </summary>
    /// <param name="vendorId">Optional vendor ID filter.</param>
    /// <param name="productIds">Optional product ID filter.</param>
    /// <exception cref="UsbException">Thrown when the get device list operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    List<IUsbDeviceDescriptor> GetDeviceList(
        ushort? vendorId = default,
        HashSet<ushort>? productIds = default
    );

    /// <summary>
    /// Get the device serial number. To read the serial the device must be opened for a brief
    /// moment; unless already open. If the device is open in another process the read will fail.
    /// </summary>
    /// <exception cref="UsbException">
    /// UsbException.Code AccessDenied or IoError is typically an indication that the device
    /// is inaccessible because it's open in another process or due to lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    string GetDeviceSerial(string deviceKey);

    /// <summary>
    /// Opens the USB device without claiming any device interfaces or reading device serial.
    /// This is a non-blocking function; no requests are sent over the USB bus.
    /// </summary>
    /// <exception cref="UsbException">
    /// UsbException.Code AccessDenied or IoError is typically an indication that the device
    /// is inaccessible; because it's open in another process or because of lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    IUsbDevice OpenDevice(string deviceKey);
}
