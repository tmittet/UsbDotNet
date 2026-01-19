using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative;

/// <summary>
/// Swappable facade for libusb 1.x. Default impl: <see cref="PInvokeLibUsbApi"/>.
/// </summary>
public interface ILibUsbApi
{
    /// <summary>
    /// Initialize libusb. This function must be called before calling any other libusb function.
    /// If you do not provide an output location for a context pointer, a default context will be
    /// created. If there was already a default context, it will be reused.
    ///
    /// NOTE: Deprecated function. Equivalent to calling libusb_init_context with no options.
    /// </summary>
    libusb_error libusb_init(out IntPtr ctx);

    /// <summary>
    /// Deinitialize libusb.
    /// </summary>
    void libusb_exit(IntPtr ctx);

    /// <summary>
    /// Set an option in the library. Use this function to configure a specific option. Some options
    /// require one or more arguments to be provided. Consult each option's documentation for
    /// specific requirements. If the context ctx is NULL, the option will be added to a list of
    /// default options that will be applied to all subsequently created contexts.
    /// </summary>
    libusb_error libusb_set_option(IntPtr ctx, libusb_option usbOption, int value);

    /// <summary>
    /// Set an option in the library. Use this function to configure a specific option. Some options
    /// require one or more arguments to be provided. Consult each option's documentation for
    /// specific requirements. If the context ctx is NULL, the option will be added to a list of
    /// default options that will be applied to all subsequently created contexts.
    /// </summary>
    libusb_error libusb_set_option(IntPtr ctx, libusb_option usbOption, IntPtr value);

    /// <summary>
    /// Handle any pending events in blocking mode. Like libusb_handle_events(), With a completed
    /// parameter to allow for race free waiting for the completion of a specific transfer.
    /// </summary>
    libusb_error libusb_handle_events_completed(IntPtr ctx, IntPtr completed);

    /// <summary>
    /// Interrupt any active thread that is handling events. This is mainly useful for interrupting
    /// a dedicated event handling thread when an application wishes to call libusb_exit().
    /// </summary>
    void libusb_interrupt_event_handler(IntPtr ctx);

    /// <summary>
    /// Returns a pointer to const struct libusb_version with the version
    /// (major, minor, micro, nano and rc) of the running library.
    /// </summary>
    IntPtr libusb_get_version();

    /// <summary>
    /// Check at runtime if the loaded library has a given capability. This call should be performed
    /// after libusb_init_context(), to ensure the backend has updated its capability set.
    /// </summary>
    int libusb_has_capability(libusb_capability capability);

    /// <summary>
    /// Returns a constant string with a short description of the given error code, this description
    /// is intended for displaying to the end user and will be in the language set by
    /// libusb_setlocale(). The returned string is encoded in UTF-8. The messages always start with
    /// a capital letter and end without any dot. The caller must not free() the returned string.
    /// </summary>
    IntPtr libusb_strerror(libusb_error errorCode);

    /// <summary>
    /// Returns a list of USB devices currently attached to the system. This is your entry point
    /// into finding a USB device to operate. You are expected to unreference all the devices when
    /// you are done with them, and then free the list with libusb_free_device_list(). Note that
    /// libusb_free_device_list() can unref all the devices for you. Be careful not to unreference
    /// a device you are about to open until after you have opened it.
    /// This return value of this function indicates the number of devices in the resultant list.
    /// The list is actually one element larger, as it is NULL-terminated.
    /// </summary>
    libusb_error libusb_get_device_list(IntPtr ctx, out IntPtr list);

    /// <summary>
    /// Frees a list of devices previously discovered using libusb_get_device_list().
    ///
    /// NOTE: If the unref_devices parameter is set, the reference count of each device
    /// in the list is decremented by 1.
    /// </summary>
    /// <param name="list">The list to free.</param>
    /// <param name="unref_devices">Whether to unref the devices in the list; 0 or 1.</param>
    void libusb_free_device_list(IntPtr list, int unref_devices);

    /// <summary>
    /// Increment the reference count of a device.
    /// </summary>
    void libusb_ref_device(IntPtr dev);

    /// <summary>
    /// Decrement the reference count of a device. If the decrement operation causes the reference
    /// count to reach zero, the device shall be destroyed.
    /// </summary>
    void libusb_unref_device(IntPtr dev);

    /// <summary>
    /// Get the USB device descriptor for a given device.
    /// This is a non-blocking function; the device descriptor is cached in memory.
    ///
    /// NOTE: Since libusb-1.0.16, this function always succeeds.
    /// </summary>
    libusb_error libusb_get_device_descriptor(IntPtr dev, out libusb_device_descriptor desc);

    /// <summary>
    /// Get the USB configuration descriptor for the currently active configuration. This is
    /// a non-blocking function which does not involve any requests being sent to the device.
    /// </summary>
    libusb_error libusb_get_active_config_descriptor(IntPtr dev, out IntPtr config);

    /// <summary>
    /// Get a USB configuration descriptor based on its index. This is a non-blocking function
    /// which does not involve any requests being sent to the device.
    /// </summary>
    libusb_error libusb_get_config_descriptor(IntPtr dev, ushort index, out IntPtr config);

    /// <summary>
    /// Free a configuration descriptor obtained from
    /// libusb_get_active_config_descriptor() or libusb_get_config_descriptor()
    /// </summary>
    void libusb_free_config_descriptor(IntPtr config);

    /// <summary>
    /// Get the number of the bus that a device is connected to.
    /// </summary>
    byte libusb_get_bus_number(IntPtr dev);

    /// <summary>
    /// Get the address of the device on the bus it is connected to.
    /// </summary>
    byte libusb_get_device_address(IntPtr dev);

    /// <summary>
    /// Get the number of the port that a device is connected to.
    ///
    /// The number returned by this call is usually guaranteed to be uniquely tied to a physical
    /// port, meaning that different devices plugged on the same physical port should return the
    /// same port number. But there is no guarantee that the port number returned by this call will
    /// remain the same, or even match the order in which ports are numbered on the HUB/HCD.
    /// </summary>
    byte libusb_get_port_number(IntPtr dev);

    /// <summary>
    /// Open a device and obtain a device handle. Allows you to perform I/O on the device.
    /// </summary>
    libusb_error libusb_open(IntPtr dev, out IntPtr handle);

    /// <summary>
    /// Close a device handle. Should be called on all open handles before your application exits.
    /// Internally, this function destroys the reference that was added by libusb_open().
    /// </summary>
    void libusb_close(IntPtr handle);

    /// <summary>
    /// Claim an interface on a given device handle. You must claim the interface you wish to use
    /// before you can perform I/O on any of its endpoints. It is legal to attempt to claim an
    /// already-claimed interface, in which case libusb just returns 0 without doing anything.
    /// If auto_detach_kernel_driver is set to 1 for dev, the kernel driver will be detached
    /// if necessary, on failure the detach error is returned. Claiming of interfaces is a purely
    /// logical operation; it does not cause any requests to be sent over the bus.Interface claiming
    /// is used to instruct the underlying operating system that your application wishes to take
    /// ownership of the interface.
    /// </summary>
    libusb_error libusb_claim_interface(IntPtr handle, byte interface_number);

    /// <summary>
    /// Release an interface previously claimed with libusb_claim_interface(). You should release
    /// all claimed interfaces before closing a device handle. This is a blocking function.
    /// A SET_INTERFACE control request will be sent to the device, resetting interface state to the
    /// first alternate setting. If auto_detach_kernel_driver is set to 1 for dev, the kernel driver
    /// will be re-attached after releasing the interface.
    /// </summary>
    libusb_error libusb_release_interface(IntPtr handle, byte interface_number);

    /// <summary>
    /// Wrapper around libusb_get_string_descriptor(). Uses the first language supported by the
    /// device. The function formulates the appropriate control message to retrieve the descriptor,
    /// and converts the Unicode string returned by the device to ASCII.
    /// </summary>
    libusb_error libusb_get_string_descriptor_ascii(
        IntPtr handle,
        byte desc_index,
        byte[] data,
        int length
    );

    /// <summary>
    /// Perform a USB port reset to reinitialize a device. The system will attempt to restore the
    /// previous configuration and alternate settings after the reset has completed. If the reset
    /// fails, the descriptors change, or the previous state cannot be restored, the device will
    /// appear to be disconnected and reconnected. This means that the device handle is no longer
    /// valid (you should close it) and rediscover the device. A return code of
    /// LIBUSB_ERROR_NOT_FOUND indicates when this is the case.
    ///
    /// NOTE: This is a blocking function which usually incurs a noticeable delay.
    /// </summary>
    libusb_error libusb_reset_device(IntPtr handle);

    /// <summary>
    /// Allocate a libusb transfer with a specified number of isochronous packet descriptors. The
    /// returned transfer is pre-initialized for you. When the new transfer is no longer needed, it
    /// should be freed with libusb_free_transfer().
    ///
    /// Transfers intended for non-isochronous endpoints (e.g. control, bulk, interrupt) should
    /// specify an iso_packets count of zero. For transfers intended for isochronous endpoints,
    /// specify an appropriate number of packet descriptors to be allocated as part of the transfer.
    /// The returned transfer is not specially initialized for isochronous I/O; you are still
    /// required to set the num_iso_packets and type fields accordingly.
    ///
    /// It is safe to allocate a transfer with some isochronous packets and then use it on a
    /// non-isochronous endpoint. If you do this, ensure that at time of submission, num_iso_packets
    /// is 0 and that type is set appropriately.
    /// </summary>
    IntPtr libusb_alloc_transfer(int iso_packets);

    /// <summary>
    /// Free a transfer structure. This should be called for all transfers allocated with
    /// libusb_alloc_transfer(). If the LIBUSB_TRANSFER_FREE_BUFFER flag is set and the transfer
    /// buffer is non-NULL, this function will also free the transfer buffer using the standard
    /// system memory allocator(e.g.free()). It is legal to call this function with a NULL transfer.
    /// In this case, the function will simply return safely. It is not legal to free an active
    /// transfer (one which has been submitted and has not yet completed).
    /// </summary>
    void libusb_free_transfer(IntPtr transfer);

    /// <summary>
    /// Submit a transfer. This function will fire off the USB transfer and then return immediately.
    /// </summary>
    /// <returns>
    /// 0 on success<br />
    /// LIBUSB_ERROR_NO_DEVICE if the device has been disconnected.<br />
    /// LIBUSB_ERROR_BUSY if the transfer has already been submitted.<br />
    /// LIBUSB_ERROR_NOT_SUPPORTED if the transfer flags are not supported by the OS.<br />
    /// LIBUSB_ERROR_INVALID_PARAM if the transfer size is larger than the OS and/or hardware can
    /// support (see Transfer length limitations) another LIBUSB_ERROR code on other failure.<br />
    /// </returns>
    libusb_error libusb_submit_transfer(IntPtr transfer);

    /// <summary>
    /// Asynchronously cancel a previously submitted transfer. This function returns immediately,
    /// but this does not indicate cancellation is complete.Your callback function will be invoked
    /// at some later time with a transfer status of LIBUSB_TRANSFER_CANCELLED.
    ///
    /// NOTE: This function behaves differently on Darwin-based systems (macOS and iOS):
    /// Calling this function for one transfer will cause all transfers on the same endpoint to be
    /// cancelled. Your callback function will be invoked with a transfer status of
    /// LIBUSB_TRANSFER_CANCELLED for each transfer that was cancelled.
    /// </summary>
    /// <returns>
    /// LIBUSB_ERROR_NOT_FOUND if the transfer is not in progress, already complete, or already
    /// cancelled. A LIBUSB_ERROR code on failure.
    /// </returns>
    libusb_error libusb_cancel_transfer(IntPtr transfer);

    /// <summary>
    /// Register a hotplug callback function. Register a callback with the libusb_context. The
    /// callback will fire when a matching event occurs on a matching device. The callback is armed
    /// until either it is deregistered with libusb_hotplug_deregister_callback() or the supplied
    /// callback returns 1 to indicate it is finished processing events.
    /// If the LIBUSB_HOTPLUG_ENUMERATE is passed the callback will be called with a
    /// LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED for all devices already plugged into the machine. Note
    /// that libusb modifies its internal device list from a separate thread, while calling hotplug
    /// callbacks from libusb_handle_events(), so it is possible for a device to already be present
    /// on, or removed from, its internal device list, while the hotplug callbacks still need to be
    /// dispatched. This means that when using LIBUSB_HOTPLUG_ENUMERATE, your callback may be called
    /// twice for the arrival of the same device, once from libusb_hotplug_register_callback() and
    /// once from libusb_handle_events(); and/or your callback may be called for the removal of a
    /// device for which an arrived call was never made.
    /// </summary>
    libusb_error libusb_hotplug_register_callback(
        IntPtr ctx,
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        int vendorId,
        int productId,
        int devClass,
        libusb_hotplug_callback_fn cb,
        IntPtr user_data,
        out IntPtr callbackHandle
    );

    /// <summary>
    /// Deregisters a hotplug callback. Deregister a callback from a libusb_context
    /// This function is safe to call from within a hotplug callback.
    /// </summary>
    void libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle);
}
