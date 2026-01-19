using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.Internal.Transfer;

internal static class LibUsbTransferStatusExtension
{
    public static libusb_error ToLibUsbError(this libusb_transfer_status status) =>
        status switch
        {
            libusb_transfer_status.LIBUSB_TRANSFER_COMPLETED => libusb_error.LIBUSB_SUCCESS,
            libusb_transfer_status.LIBUSB_TRANSFER_ERROR => libusb_error.LIBUSB_ERROR_IO,
            libusb_transfer_status.LIBUSB_TRANSFER_TIMED_OUT => libusb_error.LIBUSB_ERROR_TIMEOUT,
            libusb_transfer_status.LIBUSB_TRANSFER_CANCELLED =>
                libusb_error.LIBUSB_ERROR_INTERRUPTED,
            libusb_transfer_status.LIBUSB_TRANSFER_STALL => libusb_error.LIBUSB_ERROR_BUSY,
            libusb_transfer_status.LIBUSB_TRANSFER_NO_DEVICE => libusb_error.LIBUSB_ERROR_NO_DEVICE,
            libusb_transfer_status.LIBUSB_TRANSFER_OVERFLOW => libusb_error.LIBUSB_ERROR_OVERFLOW,
        };
}
