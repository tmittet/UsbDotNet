using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative;

// TODO: Research using DefaultDllImportSearchPaths attribute reduce risk of DLL preloading attacks
// SYSLIB1054: LibraryImportAttribute not available in .NET6, silence until removal of .NET6 support
#pragma warning disable CA5392 // CA5392: Use DefaultDllImportSearchPaths attribute for P/Invokes

/// <summary>Concrete ILibUsbApi using P/Invoke.</summary>
public sealed
#if NET7_0_OR_GREATER
partial
#endif
class PInvokeLibUsbApi : ILibUsbApi
{
    private const string Lib = "libusb-1.0";

#if NET7_0_OR_GREATER

    #region Context/Options

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_init(out IntPtr ctx);

    [LibraryImport(Lib)]
    private static partial void libusb_exit(IntPtr ctx);

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_set_option(IntPtr ctx, int option, int value);

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_set_option(IntPtr ctx, int option, IntPtr value);

    [LibraryImport(Lib)]
    private static partial int libusb_handle_events_completed(IntPtr ctx, IntPtr completed);

    [LibraryImport(Lib)]
    private static partial void libusb_interrupt_event_handler(IntPtr ctx);

    [LibraryImport(Lib)]
    private static partial IntPtr libusb_get_version();

    [LibraryImport(Lib)]
    private static partial int libusb_has_capability(libusb_capability capability);

    [LibraryImport(Lib)]
    private static partial IntPtr libusb_strerror(libusb_error errcode);

    #endregion

    #region Device list/refs

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_device_list(IntPtr ctx, out IntPtr list);

    [LibraryImport(Lib)]
    private static partial void libusb_free_device_list(IntPtr list, int unrefDevices);

    [LibraryImport(Lib)]
    private static partial IntPtr libusb_ref_device(IntPtr dev);

    [LibraryImport(Lib)]
    private static partial void libusb_unref_device(IntPtr dev);

    #endregion

    #region Device metadata

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_device_descriptor(
        IntPtr dev,
        out libusb_device_descriptor d
    );

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_active_config_descriptor(
        IntPtr dev,
        out IntPtr cfg
    );

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_config_descriptor(
        IntPtr dev,
        ushort index,
        out IntPtr cfg
    );

    [LibraryImport(Lib)]
    private static partial void libusb_free_config_descriptor(IntPtr cfg);

    [LibraryImport(Lib)]
    private static partial byte libusb_get_bus_number(IntPtr dev);

    [LibraryImport(Lib)]
    private static partial byte libusb_get_device_address(IntPtr dev);

    [LibraryImport(Lib)]
    private static partial byte libusb_get_port_number(IntPtr dev);

    #endregion

    #region Open/close


    [LibraryImport(Lib)]
    private static partial libusb_error libusb_open(IntPtr dev, out IntPtr handle);

    [LibraryImport(Lib)]
    private static partial void libusb_close(IntPtr handle);

    #endregion

    #region Config/Interfaces

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_claim_interface(IntPtr h, int iface);

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_release_interface(IntPtr h, int iface);

    #endregion

    #region Strings

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_device_string(
        IntPtr dev,
        libusb_device_string_type string_type,
        byte[] data,
        int length
    );

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_get_string_descriptor_ascii(
        IntPtr h,
        byte idx,
        byte[] data,
        int len
    );

    #endregion

    #region Halt/Reset

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_reset_device(IntPtr h);

    #endregion

    #region Events/Async

    [LibraryImport(Lib)]
    private static partial IntPtr libusb_alloc_transfer(int iso);

    [LibraryImport(Lib)]
    private static partial void libusb_free_transfer(IntPtr t);

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_submit_transfer(IntPtr t);

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_cancel_transfer(IntPtr t);

    #endregion

    #region Hotplug

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_hotplug_register_callback(
        IntPtr ctx,
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        int vendor,
        int product,
        int devClass,
        libusb_hotplug_callback_fn cb,
        IntPtr user_data,
        out IntPtr callbackHandle
    );

    [LibraryImport(Lib)]
    private static partial void libusb_hotplug_deregister_callback(
        IntPtr ctx,
        IntPtr callbackHandle
    );

    #endregion

    #region Attach/Detach Kernel Driver
    [LibraryImport(Lib)]
    private static partial libusb_error libusb_set_auto_detach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_detach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    [LibraryImport(Lib)]
    private static partial libusb_error libusb_attach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    #endregion

#else

    #region Context/Options

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_init(out IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_exit(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_set_option(IntPtr ctx, int option, int value);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_set_option(IntPtr ctx, int option, IntPtr value);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_handle_events_completed(IntPtr ctx, IntPtr completed);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_interrupt_event_handler(IntPtr ctx);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_get_version();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int libusb_has_capability(libusb_capability capability);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_strerror(libusb_error errcode);

    #endregion

    #region Device list/refs

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_device_list(IntPtr ctx, out IntPtr list);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_free_device_list(IntPtr list, int unrefDevices);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_ref_device(IntPtr dev);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_unref_device(IntPtr dev);

    #endregion

    #region Device metadata

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_device_descriptor(
        IntPtr dev,
        out libusb_device_descriptor d
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_active_config_descriptor(
        IntPtr dev,
        out IntPtr cfg
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_config_descriptor(
        IntPtr dev,
        ushort index,
        out IntPtr cfg
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_free_config_descriptor(IntPtr cfg);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte libusb_get_bus_number(IntPtr dev);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte libusb_get_device_address(IntPtr dev);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte libusb_get_port_number(IntPtr dev);

    #endregion

    #region Open/close


    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_open(IntPtr dev, out IntPtr handle);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_close(IntPtr handle);

    #endregion

    #region Config/Interfaces

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_claim_interface(IntPtr h, int iface);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_release_interface(IntPtr h, int iface);

    #endregion

    #region Strings

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_device_string(
        IntPtr dev,
        libusb_device_string_type string_type,
        byte[] data,
        int length
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_get_string_descriptor_ascii(
        IntPtr h,
        byte idx,
        byte[] data,
        int len
    );

    #endregion

    #region Halt/Reset

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_reset_device(IntPtr h);

    #endregion

    #region Events/Async

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr libusb_alloc_transfer(int iso);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_free_transfer(IntPtr t);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_submit_transfer(IntPtr t);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_cancel_transfer(IntPtr t);

    #endregion

    #region Hotplug

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_hotplug_register_callback(
        IntPtr ctx,
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        int vendor,
        int product,
        int devClass,
        libusb_hotplug_callback_fn cb,
        IntPtr user_data,
        out IntPtr callbackHandle
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void libusb_hotplug_deregister_callback(
        IntPtr ctx,
        IntPtr callbackHandle
    );

    #endregion

    #region Attach/Detach Kernel Driver

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_set_auto_detach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_detach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern libusb_error libusb_attach_kernel_driver(
        IntPtr dev,
        int interface_number
    );

    #endregion

#endif

    #region Expose via interface

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_set_auto_detach_kernel_driver(
        IntPtr dev,
        int interface_number
    ) => libusb_set_auto_detach_kernel_driver(dev, interface_number);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_detach_kernel_driver(IntPtr dev, int interface_number) =>
        libusb_detach_kernel_driver(dev, interface_number);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_attach_kernel_driver(IntPtr dev, int interface_number) =>
        libusb_attach_kernel_driver(dev, interface_number);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_init(out IntPtr ctx) => libusb_init(out ctx);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_exit(IntPtr ctx) => libusb_exit(ctx);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_set_option(IntPtr ctx, libusb_option option, int value) =>
        libusb_set_option(ctx, (int)option, value);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_set_option(IntPtr ctx, libusb_option option, IntPtr value) =>
        libusb_set_option(ctx, (int)option, value);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_handle_events_completed(IntPtr ctx, IntPtr completed) =>
        (libusb_error)libusb_handle_events_completed(ctx, completed);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_interrupt_event_handler(IntPtr ctx) =>
        libusb_interrupt_event_handler(ctx);

    /// <inheritdoc/>
    IntPtr ILibUsbApi.libusb_get_version() => libusb_get_version();

    /// <inheritdoc/>
    int ILibUsbApi.libusb_has_capability(libusb_capability capability) =>
        libusb_has_capability(capability);

    /// <inheritdoc/>
    IntPtr ILibUsbApi.libusb_strerror(libusb_error errorCode) => libusb_strerror(errorCode);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_device_list(IntPtr ctx, out IntPtr list) =>
        libusb_get_device_list(ctx, out list);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_free_device_list(IntPtr list, int unrefDevices) =>
        libusb_free_device_list(list, unrefDevices);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_ref_device(IntPtr dev) => libusb_ref_device(dev);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_unref_device(IntPtr dev) => libusb_unref_device(dev);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_device_descriptor(
        IntPtr dev,
        out libusb_device_descriptor d
    ) => libusb_get_device_descriptor(dev, out d);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_active_config_descriptor(IntPtr dev, out IntPtr cfg) =>
        libusb_get_active_config_descriptor(dev, out cfg);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_config_descriptor(
        IntPtr dev,
        ushort index,
        out IntPtr cfg
    ) => libusb_get_config_descriptor(dev, index, out cfg);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_free_config_descriptor(IntPtr cfg) => libusb_free_config_descriptor(cfg);

    /// <inheritdoc/>
    byte ILibUsbApi.libusb_get_bus_number(IntPtr dev) => libusb_get_bus_number(dev);

    /// <inheritdoc/>
    byte ILibUsbApi.libusb_get_device_address(IntPtr dev) => libusb_get_device_address(dev);

    /// <inheritdoc/>
    byte ILibUsbApi.libusb_get_port_number(IntPtr dev) => libusb_get_port_number(dev);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_device_string(
        IntPtr dev,
        libusb_device_string_type string_type,
        byte[] data,
        int length
    ) => libusb_get_device_string(dev, string_type, data, length);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_open(IntPtr dev, out IntPtr h) => libusb_open(dev, out h);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_close(IntPtr h) => libusb_close(h);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_claim_interface(IntPtr h, byte i) =>
        libusb_claim_interface(h, i);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_release_interface(IntPtr h, byte i) =>
        libusb_release_interface(h, i);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_get_string_descriptor_ascii(
        IntPtr h,
        byte idx,
        byte[] data,
        int len
    ) => libusb_get_string_descriptor_ascii(h, idx, data, len);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_reset_device(IntPtr h) => libusb_reset_device(h);

    /// <inheritdoc/>
    IntPtr ILibUsbApi.libusb_alloc_transfer(int iso) => libusb_alloc_transfer(iso);

    /// <inheritdoc/>
    void ILibUsbApi.libusb_free_transfer(IntPtr t) => libusb_free_transfer(t);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_submit_transfer(IntPtr t) => libusb_submit_transfer(t);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_cancel_transfer(IntPtr t) => libusb_cancel_transfer(t);

    /// <inheritdoc/>
    libusb_error ILibUsbApi.libusb_hotplug_register_callback(
        IntPtr ctx,
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        int vendorId,
        int productId,
        int devClass,
        libusb_hotplug_callback_fn cb,
        IntPtr user_data,
        out IntPtr handle
    ) =>
        libusb_hotplug_register_callback(
            ctx,
            events,
            flags,
            vendorId,
            productId,
            devClass,
            cb,
            user_data,
            out handle
        );

    /// <inheritdoc/>
    void ILibUsbApi.libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr handle) =>
        libusb_hotplug_deregister_callback(ctx, handle);

    #endregion
}
