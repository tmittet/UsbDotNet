using System.Diagnostics.CodeAnalysis;
using UsbDotNet.LibUsbNative.Enums;
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
    /// Retrieve a device string without needing to open the device.
    /// <para>
    /// The string will be returned untranslated or in the default OS language when supported by the
    /// OS and USB device.
    /// </para>
    /// </summary>
    /// <returns>A UTF-8 encoded string.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the descriptor read operation fails.</exception>
    string GetDeviceString(libusb_device_string_type stringType);

    /// <summary>
    /// Retrieve a device string without needing to open the device.
    /// <para>
    /// The string will be returned untranslated or in the default OS language when supported by the
    /// OS and USB device.
    /// </para>
    /// </summary>
    /// <returns>
    /// True when the string read operation was successful; otherwise false with a libusb_error output.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    bool TryGetDeviceString(
        libusb_device_string_type stringType,
        [NotNullWhen(true)] out string? descriptorValue,
        [NotNullWhen(false)] out libusb_error? usbError
    );

    /// <summary>
    /// Identifier for the underlying device, stable for the lifetime of the physical device
    /// instance. libusb reuses it across hotplug arrival and removal, so it can be used to
    /// correlate the two events.
    /// <para>
    /// Stability is established by the libusb source (not stated in its API docs): in core.c,
    /// usbi_connect_device and usbi_disconnect_device pass the same libusb_device to
    /// usbi_hotplug_notification, which stores the pointer verbatim on the queued message
    /// (hotplug.c, msg->device = dev) that usbi_hotplug_process later hands to callbacks; the
    /// DEVICE_LEFT message holds a device reference that is dropped only after the callbacks have
    /// run. See https://github.com/libusb/libusb/blob/v1.0.30/libusb/core.c and
    /// https://github.com/libusb/libusb/blob/v1.0.30/libusb/hotplug.c
    /// </para>
    /// </summary>
    UniqueId Id { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
