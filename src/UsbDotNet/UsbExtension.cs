using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Extension methods for IUsb.
/// </summary>
public static class UsbExtension
{
    /// <summary>
    /// Returns a list of device descriptors for connected USB devices.
    /// It does not involve any requests being sent to the devices.
    /// </summary>
    /// <param name="usb">Usb type instance.</param>
    /// <param name="vendorId">Optional vendor ID filter.</param>
    /// <param name="productId">Optional product ID filter.</param>
    /// <exception cref="UsbException">Thrown when the get device list operation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Usb type is not initialized.</exception>
    public static IReadOnlyCollection<IUsbDeviceDescriptor> GetDeviceList(
        this IUsb usb,
        ushort? vendorId = default,
        params ushort[] productId
    ) => usb.GetDeviceList(vendorId, productId.Length == 0 ? null : productId.ToHashSet());

    /// <summary>
    /// Get the device serial number. To read the serial the device must be opened for a brief
    /// moment; unless already open. If the device is open in another process the read will fail.
    /// </summary>
    /// <exception cref="UsbException">
    /// UsbException.Code AccessDenied or IoError is typically an indication that the device
    /// is inaccessible because it's open in another process or due to lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Usb type is not initialized.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    public static string GetDeviceSerial(this IUsb usb, IUsbDeviceDescriptor descriptor) =>
        usb.GetDeviceSerial(descriptor.DeviceKey);

    /// <summary>
    /// Opens the USB device without claiming any device interfaces or reading device serial.
    /// This is a non-blocking function; no requests are sent over the USB bus.
    /// </summary>
    /// <exception cref="UsbException">
    /// UsbException.Code AccessDenied or IoError is typically an indication that the device
    /// is inaccessible because it's open in another process or due to lacking permissions.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Usb type is not initialized.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the Usb type is disposed.</exception>
    public static IUsbDevice OpenDevice(this IUsb usb, IUsbDeviceDescriptor descriptor) =>
        usb.OpenDevice(descriptor.DeviceKey);
}
