using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Main entry point for USB operations. Provides methods to initialize the USB library, enumerate
/// USB devices or register hotplug events, and open USB devices. It implements IDisposable for
/// proper cleanup of resources when the USB operations are no longer needed.
/// </summary>
public interface IUsb : IDisposable
{
    /// <summary>
    /// Initializes the USB library (libusb), attaches a log callback and starts the
    /// background thread that handles USB events and drives async transfers. The libusb
    /// native log level is read from <see cref="UsbDotNetOptions.NativeLibraryLogLevel"/>
    /// supplied at construction.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    void Initialize();

    /// <summary>
    /// Returns a list of device descriptors for connected USB devices.
    /// It does not involve any requests being sent to the devices.
    /// </summary>
    /// <param name="filter">Optional device filter; when null, every device is returned.</param>
    /// <exception cref="UsbException">Thrown when the get device list operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    IReadOnlyCollection<IUsbDeviceDescriptor> GetDeviceList(IUsbDeviceFilter? filter = null);

    /// <summary>
    /// Get the device manufacturer from the string descriptors read and cached by the OS during
    /// device enumeration. As a fallback; read the manufacturer directly from the device.
    /// </summary>
    /// <exception cref="UsbException">
    /// Thrown when the descriptor cannot be read. <see cref="UsbException.Code"/> indicates why:
    /// <see cref="UsbResult.NotFound"/> when no device with <paramref name="deviceKey"/> is present
    /// in the current system device list (for example it has been unplugged); or
    /// <see cref="UsbResult.AccessDenied"/> / <see cref="UsbResult.IoError"/>, typically because
    /// the device is inaccessible due to being open in another process or lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <returns>
    /// The manufacturer, or an empty string when the device has no manufacturer string.
    /// </returns>
    string GetDeviceManufacturer(string deviceKey);

    /// <summary>
    /// Get the device product name from the string descriptors read and cached by the OS during
    /// device enumeration. As a fallback; read the product name directly from the device.
    /// </summary>
    /// <exception cref="UsbException">
    /// Thrown when the descriptor cannot be read. <see cref="UsbException.Code"/> indicates why:
    /// <see cref="UsbResult.NotFound"/> when no device with <paramref name="deviceKey"/> is present
    /// in the current system device list (for example it has been unplugged); or
    /// <see cref="UsbResult.AccessDenied"/> / <see cref="UsbResult.IoError"/>, typically because
    /// the device is inaccessible due to being open in another process or lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <returns>
    /// The product name, or an empty string when the device has no product name string.
    /// </returns>
    string GetDeviceProduct(string deviceKey);

    /// <summary>
    /// Get the device serial number from the string descriptors read and cached by the OS during
    /// device enumeration. As a fallback; read the serial directly from the device.
    /// </summary>
    /// <exception cref="UsbException">
    /// Thrown when the descriptor cannot be read. <see cref="UsbException.Code"/> indicates why:
    /// <see cref="UsbResult.NotFound"/> when no device with <paramref name="deviceKey"/> is present
    /// in the current system device list (for example it has been unplugged); or
    /// <see cref="UsbResult.AccessDenied"/> / <see cref="UsbResult.IoError"/>, typically because
    /// the device is inaccessible due to being open in another process or lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <returns>
    /// The serial number, or an empty string when the device has no serial number string.
    /// </returns>
    string GetDeviceSerialNumber(string deviceKey);

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
