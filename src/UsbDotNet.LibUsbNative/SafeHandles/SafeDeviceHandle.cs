using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeDeviceHandle : SafeHandle, ISafeDeviceHandle
{
    private readonly SafeContext _context;
#pragma warning disable CA2213 // Disposable fields should be disposed
    // SafeDevice disposed in ReleaseHandle
    private readonly SafeDevice _device;
#pragma warning restore CA2213 // Disposable fields should be disposed

    internal ILibUsbApi Api => _context.Api;

    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <inheritdoc/>
    public ISafeDevice Device
    {
        get
        {
            SafeHelper.ThrowIfClosed(this);
            return _device;
        }
    }

    public SafeDeviceHandle(SafeContext context, nint deviceHandle, nint devicePtr)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        if (deviceHandle == IntPtr.Zero)
            throw new ArgumentNullException(nameof(deviceHandle));

        _context = context;
        // Create a new SafeDevice here with a lifetime that's owned by the SafeDeviceHandle.
        // SafeContext ref count is decremented to "release" devicePtr when SafeDevice is disposed.
        _device = new SafeDevice(_context, devicePtr);
        handle = deviceHandle;
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        _context.Api.libusb_close(handle);
        // There is no need to decrement any reference counters here, by calling DangerousRelease,
        // SafeContext ref count is decremented to "release" devicePtr when SafeDevice is disposed.
        _device.Dispose();
        return true;
    }

    // GET_DESCRIPTOR for a string descriptor; USB 2.0 specification section 9.4.3 and 9.6.7
    private const byte RequestTypeDeviceToHost = 0x80;
    private const byte RequestGetDescriptor = 0x06;
    private const byte DescriptorTypeString = 0x03;
    private const int DescriptorHeaderLength = 2;
    private const int StringDescriptorMaxLength = 255;
    private const int LanguageIdDescriptorLength = 4;

    /// <summary>
    /// The timeout libusb uses for its own string descriptor reads, in milliseconds.
    /// </summary>
    private const uint StringDescriptorTimeout = 1000;

    /// <inheritdoc/>
    public string GetStringDescriptor(byte index)
    {
        return TryGetStringDescriptor(index, out var value, out var error)
            ? value
            : throw error.Value.ToLibUsbExceptionForApi(
                nameof(_context.Api.libusb_control_transfer),
                $"String descriptor {index}."
            );
    }

    /// <inheritdoc/>
    public bool TryGetStringDescriptor(
        byte index,
        [NotNullWhen(true)] out string? descriptorValue,
        [NotNullWhen(false)] out libusb_error? usbError
    )
    {
        using var reference = SafeHelper.AcquireReference(this);

        // This follows libusb_get_string_descriptor_ascii, except for the conversion to ASCII
        descriptorValue = null;
        // Index zero is not a string; it holds the language IDs supported by the device
        if (index == 0)
        {
            usbError = libusb_error.LIBUSB_ERROR_INVALID_PARAM;
            return false;
        }

        // Read the first language ID supported by the device
        var buffer = new byte[StringDescriptorMaxLength];
        var result = ReadStringDescriptor(0, 0, buffer, LanguageIdDescriptorLength);
        if (result < 0)
        {
            usbError = result;
            return false;
        }
        if (
            (int)result != LanguageIdDescriptorLength
            || buffer[0] < LanguageIdDescriptorLength
            || buffer[1] != DescriptorTypeString
        )
        {
            usbError = libusb_error.LIBUSB_ERROR_IO;
            return false;
        }
        var languageId = (ushort)(buffer[2] | (buffer[3] << 8));

        // Read the string in that language
        result = ReadStringDescriptor(index, languageId, buffer, StringDescriptorMaxLength);
        if (result < 0)
        {
            usbError = result;
            return false;
        }
        var bytesRead = (int)result;
        if (
            bytesRead < DescriptorHeaderLength
            || buffer[0] > bytesRead
            || buffer[0] < DescriptorHeaderLength
            || buffer[1] != DescriptorTypeString
        )
        {
            usbError = libusb_error.LIBUSB_ERROR_IO;
            return false;
        }

        // The string is UTF-16LE, without a terminator; an odd trailing byte is ignored
        var stringLength = (buffer[0] - DescriptorHeaderLength) & ~1;
        descriptorValue = Encoding.Unicode.GetString(buffer, DescriptorHeaderLength, stringLength);
        usbError = null;
        return true;
    }

    private libusb_error ReadStringDescriptor(
        byte index,
        ushort languageId,
        byte[] buffer,
        int length
    ) =>
        _context.Api.libusb_control_transfer(
            handle,
            RequestTypeDeviceToHost,
            RequestGetDescriptor,
            (ushort)((DescriptorTypeString << 8) | index),
            languageId,
            buffer,
            (ushort)length,
            StringDescriptorTimeout
        );

    /// <inheritdoc/>
    public string GetStringDescriptorAscii(byte index)
    {
        return TryGetStringDescriptorAscii(index, out var value, out var error)
            ? value
            : throw error.Value.ToLibUsbExceptionForApi(
                nameof(_context.Api.libusb_get_string_descriptor_ascii)
            );
    }

    /// <inheritdoc/>
    public bool TryGetStringDescriptorAscii(
        byte index,
        [NotNullWhen(true)] out string? descriptorValue,
        [NotNullWhen(false)] out libusb_error? usbError
    )
    {
        using var reference = SafeHelper.AcquireReference(this);

        var buffer = new byte[256];
        var result = _context.Api.libusb_get_string_descriptor_ascii(
            handle,
            index,
            buffer,
            buffer.Length
        );

        if (result >= 0)
        {
            descriptorValue = Encoding.ASCII.GetString(buffer, 0, (int)result);
            usbError = null;
            return true;
        }

        descriptorValue = null;
        usbError = result;
        return false;
    }

    public ISafeDeviceInterface ClaimInterface(byte interfaceNumber)
    {
        using var reference = SafeHelper.AcquireReference(this);

        var result = _context.Api.libusb_claim_interface(handle, interfaceNumber);
        result.ThrowLibUsbExceptionForApi(
            nameof(_context.Api.libusb_claim_interface),
            $"Interface {interfaceNumber}."
        );

        // Increment the device handle ref counter, SafeDeviceInterface will decrement on dispose.
        // The device is not closed until its interfaces are released.
        var success = false;
        DangerousAddRef(ref success);
        if (!success)
        {
            _ = _context.Api.libusb_release_interface(handle, interfaceNumber);
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException("Failed to ref SafeHandle.");
        }
        return new SafeDeviceInterface(this, interfaceNumber);
    }

    /// <inheritdoc/>
    public void ResetDevice()
    {
        using var reference = SafeHelper.AcquireReference(this);
        var result = _context.Api.libusb_reset_device(handle);
        result.ThrowLibUsbExceptionForApi(nameof(_context.Api.libusb_reset_device));
    }

    /// <inheritdoc/>
    public ISafeTransfer AllocateTransfer(int isoPackets = 0)
    {
        if (isoPackets < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(isoPackets),
                "Must be greater than or equal to zero."
            );
        }

        var ptr = _context.Api.libusb_alloc_transfer(isoPackets);
        return ptr == IntPtr.Zero
            ? throw libusb_error.LIBUSB_ERROR_NO_MEM.ToLibUsbException(
                $"LibUsbApi '{nameof(_context.Api.libusb_alloc_transfer)}' failed."
            )
            : (ISafeTransfer)new SafeTransfer(_context, ptr);
    }

    /// <summary>
    /// Attempt to log a message using the registered log handler; if there is one.
    /// </summary>
    internal void Log(libusb_log_level level, string message) => _context.Log(level, message);
}
