using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Internal.Transfer;

internal static class LibUsbTransfer
{
    /// <summary>
    /// Synchronously create, submit and wait for a transfer to complete, be canceled or fail.
    /// NOTE: On macOS, cancelling a transfer may cancel all transfers on specified endpoint.
    /// </summary>
    public static libusb_error ExecuteSync(
        ILogger logger,
        ISafeDeviceHandle deviceHandle,
        libusb_endpoint_transfer_type transferType,
        byte endpointAddress,
        GCHandle bufferHandle,
        int bufferLength,
        uint timeout,
        out int bytesTransferred,
        CancellationToken ct
    )
    {
        bytesTransferred = 0;
        if (ct.IsCancellationRequested)
        {
            return libusb_error.LIBUSB_ERROR_INTERRUPTED;
        }

        using var transferCompleteEvent = new ManualResetEvent(false);

        GCHandle callbackHandle = default;
        var transferPtr = IntPtr.Zero;
        var transferStatus = (int)libusb_transfer_status.LIBUSB_TRANSFER_ERROR;
        var transferLength = 0;
        try
        {
            // Create native callback and pin it so the delegate isn't GC'd in flight
            libusb_transfer_cb_fn nativeCallback = (ptr) =>
            {
                var transfer = Marshal.PtrToStructure<libusb_transfer>(ptr);
                Volatile.Write(ref transferStatus, (int)transfer.status);
                Volatile.Write(ref transferLength, transfer.actual_length);
                _ = transferCompleteEvent.Set();
            };
            callbackHandle = GCHandle.Alloc(nativeCallback);

            // Allocate and initialize the libusb transfer
            using var transferBuffer = deviceHandle.AllocateTransfer(0);
            transferPtr = transferBuffer.GetBufferPtr();

            // libusb_alloc_transfer returns zero pointer on error
            if (transferPtr == IntPtr.Zero)
            {
                return libusb_error.LIBUSB_ERROR_OTHER;
            }
            var transferTemplate = libusb_transfer.Create(
                deviceHandle,
                endpointAddress,
                bufferHandle,
                bufferLength,
                transferType,
                timeout,
                nativeCallback
            );
            Marshal.StructureToPtr(transferTemplate, transferPtr, false);

#if DEBUG
            logger.LogTrace("Submitting transfer: {Transfer}.", transferTemplate);
#endif
            // Submit the USB transfer and then return immediately.
            // The registered LibUsbTransferCallback is invoked on completion.
            var submitResult = transferBuffer.Submit();
            if (submitResult is not libusb_error.LIBUSB_SUCCESS)
            {
                return submitResult;
            }

            // Wait for transfer complete or cancellation. If transfer complete is not signaled;
            // we tell libusb to cancel the transfer and wait for the cancellation to complete.
            if (WaitHandle.WaitAny(new[] { transferCompleteEvent, ct.WaitHandle }) != 0)
            {
                // Tell libusb to cancel the transfer, the final transfer status
                // is received through the LibUsbTransferCallback.
                var cancelResult = transferBuffer.Cancel();
                if (
                    cancelResult
                    is not libusb_error.LIBUSB_ERROR_NO_DEVICE
                        and not libusb_error.LIBUSB_ERROR_NOT_FOUND
                        and not libusb_error.LIBUSB_SUCCESS
                )
                {
                    logger.LogError(
                        "Failed to cancel LibUsb transfer. {ErrorMessage}.",
                        cancelResult.GetMessage()
                    );
                }
                // We should not free the transfer or handle if there is still a chance
                // that the callback is triggered, doing so may result in use-after-free.
                // To avoid this, we wait indefinitely for completion or cancellation.
                // See: https://libusb.sourceforge.io/api-1.0/group__libusb__asyncio.html
                _ = transferCompleteEvent.WaitOne();
            }

            Debug.Assert(
                transferBuffer.Cancel() == libusb_error.LIBUSB_ERROR_NOT_FOUND,
                "libusb_cancel_transfer should return LIBUSB_ERROR_NOT_FOUND, after transfer complete event."
            );

            // The transfer is complete, canceled or failed; map status to result and return
            bytesTransferred = Volatile.Read(ref transferLength);
            return ((libusb_transfer_status)Volatile.Read(ref transferStatus)).ToLibUsbError();
        }
        finally
        {
            if (callbackHandle.IsAllocated)
            {
                callbackHandle.Free();
            }
        }
    }
}
