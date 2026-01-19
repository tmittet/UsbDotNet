using System.Diagnostics;
using UsbDotNet.Core;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Extensions;

public static class libusb_error_Extension
{
    internal const string UnknownLibUsbErrorMessagePrefix = "Unknown libusb error";

    /// <summary>
    /// Managed libusb error -> message mapping (avoids native libusb_error call).
    /// The messages always start with a capital letter and end without any dot.
    /// </summary>
    public static string GetMessage(this libusb_error error) =>
        error switch
        {
            libusb_error.LIBUSB_SUCCESS => "Success",
            libusb_error.LIBUSB_ERROR_IO => "Input/Output Error",
            libusb_error.LIBUSB_ERROR_INVALID_PARAM => "Invalid parameter",
            libusb_error.LIBUSB_ERROR_ACCESS => "Access denied (insufficient permissions)",
            libusb_error.LIBUSB_ERROR_NO_DEVICE => "No such device (it may have been disconnected)",
            libusb_error.LIBUSB_ERROR_NOT_FOUND => "Entity not found",
            libusb_error.LIBUSB_ERROR_BUSY => "Resource busy",
            libusb_error.LIBUSB_ERROR_TIMEOUT => "Operation timed out",
            libusb_error.LIBUSB_ERROR_OVERFLOW => "Overflow",
            libusb_error.LIBUSB_ERROR_PIPE => "Pipe error",
            libusb_error.LIBUSB_ERROR_INTERRUPTED =>
                "System call interrupted (perhaps due to signal)",
            libusb_error.LIBUSB_ERROR_NO_MEM => "Insufficient memory",
            libusb_error.LIBUSB_ERROR_NOT_SUPPORTED =>
                "Operation not supported or unimplemented on this platform",
            libusb_error.LIBUSB_ERROR_OTHER => "Other error",
            _ => $"{UnknownLibUsbErrorMessagePrefix} {error}",
        };

    public static UsbResult ToUsbResult(this libusb_error libusbError) =>
        libusbError switch
        {
            libusb_error.LIBUSB_SUCCESS => UsbResult.Success,
            libusb_error.LIBUSB_ERROR_IO => UsbResult.IoError,
            libusb_error.LIBUSB_ERROR_INVALID_PARAM => UsbResult.InvalidParameter,
            libusb_error.LIBUSB_ERROR_ACCESS => UsbResult.AccessDenied,
            libusb_error.LIBUSB_ERROR_NO_DEVICE => UsbResult.NoDevice,
            libusb_error.LIBUSB_ERROR_NOT_FOUND => UsbResult.NotFound,
            libusb_error.LIBUSB_ERROR_BUSY => UsbResult.ResourceBusy,
            libusb_error.LIBUSB_ERROR_TIMEOUT => UsbResult.Timeout,
            libusb_error.LIBUSB_ERROR_OVERFLOW => UsbResult.Overflow,
            libusb_error.LIBUSB_ERROR_PIPE => UsbResult.PipeError,
            libusb_error.LIBUSB_ERROR_INTERRUPTED => UsbResult.Interrupted,
            libusb_error.LIBUSB_ERROR_NO_MEM => UsbResult.InsufficientMemory,
            libusb_error.LIBUSB_ERROR_NOT_SUPPORTED => UsbResult.NotSupported,
            libusb_error.LIBUSB_ERROR_OTHER => UsbResult.OtherError,
        };

    internal static LibUsbException ToLibUsbException(this libusb_error error, string message)
    {
        var libusbMessage = $"{error}: {error.GetMessage()}.";
        return new(
            error,
            string.IsNullOrWhiteSpace(message) ? libusbMessage : $"{message} {libusbMessage}"
        );
    }

    internal static LibUsbException ToLibUsbExceptionForApi(
        this libusb_error error,
        string methodName,
        string? additionalInfo = null
    )
    {
        var libusbMessage = $"{error}: {error.GetMessage()}.";
        var message = $"LibUsbApi '{methodName}' failed. {additionalInfo}".TrimEnd();
        return new(error, $"{message} {libusbMessage}");
    }

    [StackTraceHidden]
    internal static void ThrowLibUsbExceptionForApi(
        this libusb_error result,
        string methodName,
        string? additionalInfo = null
    )
    {
        if (result >= 0)
        {
            return;
        }
        throw ToLibUsbExceptionForApi(result, methodName, additionalInfo);
    }
}
