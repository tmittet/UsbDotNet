using System.Diagnostics.CodeAnalysis;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.SafeHandles;

public interface ISafeDeviceHandle : IDisposable
{
    /// <summary>
    /// The owner (parent) device of the safe device handle.
    /// </summary>
    ISafeDevice Device { get; }

    nint DangerousGetHandle();

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the
    /// device. The string is returned as sent by the device; no character is replaced.
    /// </summary>
    /// <param name="index">
    /// The index of the string descriptor. Index 0 is not a string; it holds the language IDs.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the descriptor read operation fails.</exception>
    string GetStringDescriptor(byte index);

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the
    /// device. The string is returned as sent by the device; no character is replaced.
    /// </summary>
    /// <param name="index">
    /// The index of the string descriptor. Index 0 is not a string; it holds the language IDs.
    /// </param>
    /// <param name="descriptorValue">The string, when the read operation was successful.</param>
    /// <param name="usbError">The error, when the read operation failed.</param>
    /// <returns>
    /// True when the string read operation was successful; otherwise false with a libusb_error output.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    bool TryGetStringDescriptor(
        byte index,
        [NotNullWhen(returnValue: true)] out string? descriptorValue,
        [NotNullWhen(returnValue: false)] out libusb_error? usbError
    );

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the device.
    /// Characters that are not ASCII are replaced with '?'; use
    /// <see cref="GetStringDescriptor"/> to read the string as sent by the device.
    /// NOTE: On some devices it may fail even for basic fields like serial number (at index 0).
    /// </summary>
    /// <returns>
    /// True when the string read operation was successful; otherwise false with a libusb_error output.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the descriptor read operation fails.</exception>
    string GetStringDescriptorAscii(byte index);

    /// <summary>
    /// Reads a string descriptor from the device, using the first language supported by the device.
    /// Characters that are not ASCII are replaced with '?'; use
    /// <see cref="TryGetStringDescriptor"/> to read the string as sent by the device.
    /// NOTE: On some devices it may fail even for basic fields like serial number (at index 0).
    /// </summary>
    /// <returns>
    /// True when the string read operation was successful; otherwise false with a libusb_error output.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    bool TryGetStringDescriptorAscii(
        byte index,
        [NotNullWhen(returnValue: true)] out string? descriptorValue,
        [NotNullWhen(returnValue: false)] out libusb_error? usbError
    );

    /// <summary>
    /// Claim a USB device interface.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the USB device interface claim operation fails.</exception>
    ISafeDeviceInterface ClaimInterface(byte interfaceNumber);

    /// <summary>
    /// WARNING: Use very carefully! Performs a USB port reset to reconnect/reinitialize the device.
    /// The system will attempt to restore the previous configuration and alternate settings after
    /// the reset has completed. If the reset fails, the descriptors change, or the previous state
    /// cannot be restored, the device will appear to be disconnected and reconnected.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the reset operation fails.</exception>
    void ResetDevice();

    /// <summary>
    /// Clear the halt/stall condition for an endpoint. Endpoints with halt status are unable to
    /// receive or transmit data until the halt condition is cleared. Cancel all pending transfers
    /// on the endpoint before clearing the halt condition.
    /// </summary>
    /// <param name="endpointAddress">The address of the endpoint, including its direction.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the SafeDeviceHandle is disposed.</exception>
    /// <exception cref="LibUsbException">Thrown when the clear halt operation fails.</exception>
    void ClearHalt(byte endpointAddress);

    /// <summary>
    /// Allocate a libusb transfer with a specified number of isochronous packet descriptors.
    /// </summary>
    /// <param name="isoPackets">Number of isochronous packet descriptors to allocate. Must be non-negative.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when isoPackets is below zero.</exception>
    /// <exception cref="LibUsbException">Thrown with usbError LIBUSB_ERROR_NO_MEM when allocation failed.</exception>
    ISafeTransfer AllocateTransfer(int isoPackets = 0);

    /// <summary>
    /// Gets a value indicating whether the underlying handle is closed or not.
    /// NOTE: Even though the safe type is disposed, the handle may remain open.
    /// </summary>
    bool IsClosed { get; }
}
