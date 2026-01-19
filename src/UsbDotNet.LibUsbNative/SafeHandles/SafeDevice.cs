using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.SafeHandles;

internal sealed class SafeDevice : SafeHandle, ISafeDevice
{
    private readonly SafeContext _context;

    internal ILibUsbApi Api => _context.Api;

    public override bool IsInvalid => handle == IntPtr.Zero;

    public SafeDevice(SafeContext context, nint devicePtr)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        if (devicePtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(devicePtr));

        _context = context;
        _context.Api.libusb_ref_device(devicePtr);
        handle = devicePtr;
    }

    protected override bool ReleaseHandle()
    {
        if (IsInvalid)
            return true;

        _context.Api.libusb_unref_device(handle);
        _context.DangerousRelease();
        return true;
    }

    /// <inheritdoc />
    public libusb_device_descriptor GetDeviceDescriptor()
    {
        SafeHelper.ThrowIfClosed(this);

        var result = _context.Api.libusb_get_device_descriptor(handle, out var descriptor);
        // NOTE: No exception should be thown here; Since libusb-1.0.16, libusb_get_device_descriptor always succeeds.
        result.ThrowLibUsbExceptionForApi(nameof(_context.Api.libusb_get_device_descriptor));
        return descriptor;
    }

    /// <inheritdoc />
    public libusb_config_descriptor GetActiveConfigDescriptor()
    {
        SafeHelper.ThrowIfClosed(this);

        var result = _context.Api.libusb_get_active_config_descriptor(handle, out var descriptor);
        result.ThrowLibUsbExceptionForApi(nameof(_context.Api.libusb_get_active_config_descriptor));
        try
        {
            return FromPointer(descriptor);
        }
        finally
        {
            _context.Api.libusb_free_config_descriptor(descriptor);
        }
    }

    /// <inheritdoc />
    public libusb_config_descriptor GetConfigDescriptor(byte config_index)
    {
        SafeHelper.ThrowIfClosed(this);

        var result = _context.Api.libusb_get_config_descriptor(
            handle,
            config_index,
            out var descriptor
        );
        result.ThrowLibUsbExceptionForApi(nameof(_context.Api.libusb_get_config_descriptor));
        try
        {
            return FromPointer(descriptor);
        }
        finally
        {
            _context.Api.libusb_free_config_descriptor(descriptor);
        }
    }

    /// <inheritdoc />
    public byte GetBusNumber()
    {
        SafeHelper.ThrowIfClosed(this);
        return _context.Api.libusb_get_bus_number(handle);
    }

    /// <inheritdoc />
    public byte GetDeviceAddress()
    {
        SafeHelper.ThrowIfClosed(this);
        return _context.Api.libusb_get_device_address(handle);
    }

    /// <inheritdoc />
    public byte GetPortNumber()
    {
        SafeHelper.ThrowIfClosed(this);
        return _context.Api.libusb_get_port_number(handle);
    }

    /// <inheritdoc />
    public ISafeDeviceHandle Open()
    {
        SafeHelper.ThrowIfClosed(this);

        var result = _context.Api.libusb_open(handle, out var deviceHandle);
        result.ThrowLibUsbExceptionForApi(nameof(_context.Api.libusb_open));

        // Ref counter for context incremented here, not the SafeDevice ref counter.
        // This is intentional since the device pointer is "owned" by the context,
        // it's not related to something that was created by this SafeDevice.
        var success = false;
        _context.DangerousAddRef(ref success);
        if (!success)
        {
            _context.Api.libusb_close(deviceHandle);
            throw libusb_error.LIBUSB_ERROR_OTHER.ToLibUsbException("Failed to ref SafeHandle.");
        }
        return new SafeDeviceHandle(_context, deviceHandle, handle);
    }

    private static libusb_config_descriptor FromPointer(nint pConfigDescriptor) =>
        pConfigDescriptor == IntPtr.Zero
            ? throw new ArgumentNullException(nameof(pConfigDescriptor))
            : Marshal
                .PtrToStructure<native_libusb_config_descriptor>(pConfigDescriptor)
                .ToPublicConfigDescriptor();
}
