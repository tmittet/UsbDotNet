using Microsoft.Extensions.Logging;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet;

/// <summary>
/// Main entry point for USB operations. Provides methods to initialize the USB library, enumerate
/// USB devices or register hotplug events, and open USB devices. It implements IDisposable for
/// proper cleanup of resources when the USB operations are no longer needed.
/// </summary>
public interface IUsb : IDisposable
{
    /// <summary>True when hotplug is supported on the platform.</summary>
    internal bool IsHotplugSupported { get; }

    /// <summary>
    /// Runs <paramref name="action"/> with the initialized <see cref="ISafeContext"/> under
    /// the Usb type's lifetime lock, so the context cannot be disposed while the action runs.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Usb type is not initialized.
    /// </exception>
    internal T WithInitializedContext<T>(Func<ISafeContext, T> action);

    /// <summary>
    /// Initializes the USB library (libusb), attaches a log callback and starts the
    /// background thread that handles USB events and drives async transfers. The libusb
    /// native log level is read from <see cref="UsbDotNetOptions.NativeLibraryLogLevel"/>
    /// supplied at construction.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    void Initialize();

    /// <summary>
    /// Initializes the USB library with an explicit native log level.
    /// </summary>
    /// <param name="nativeLibraryLogLevel">The desired log level for the libusb native library.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    [Obsolete(
        "Configure NativeLibraryLogLevel via UsbDotNetOptions when constructing Usb, and call the "
            + "parameterless Initialize() instead. This overload will be removed in a future version."
    )]
    void Initialize(LogLevel nativeLibraryLogLevel);

    /// <summary>
    /// Hotplug events are supported on macOS, Linux and Windows.
    /// https://libusb.sourceforge.io/api-1.0/libusb_hotplug.html
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    [Obsolete(
        "Use UsbDotNet.Hotplug package instead. This method will be removed in a future version."
    )]
    bool RegisterHotplug(
        UsbClass? deviceClass = null,
        ushort? vendorId = null,
        ushort? productId = null
    );

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
