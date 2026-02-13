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
        SafeHelper.ThrowIfClosed(this);

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
        SafeHelper.ThrowIfClosed(this);

        var result = _context.Api.libusb_claim_interface(handle, interfaceNumber);
        result.ThrowLibUsbExceptionForApi(
            nameof(_context.Api.libusb_claim_interface),
            $"Interface {interfaceNumber}."
        );
        return new SafeDeviceInterface(this, interfaceNumber);
    }

    /// <inheritdoc/>
    public void ResetDevice()
    {
        SafeHelper.ThrowIfClosed(this);
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
