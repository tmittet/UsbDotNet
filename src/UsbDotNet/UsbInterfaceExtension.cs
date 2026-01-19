using UsbDotNet.Core;

namespace UsbDotNet;

public static class UsbInterfaceExtension
{
    public static UsbResult BulkRead(
        this IUsbInterface usbInterface,
        byte[] destination,
        out int bytesRead,
        int timeout = Timeout.Infinite
    ) => usbInterface.BulkRead(destination.AsSpan(), out bytesRead, timeout);

    public static UsbResult BulkWrite(
        this IUsbInterface usbInterface,
        byte[] source,
        int count,
        out int bytesWritten,
        int timeout = Timeout.Infinite
    ) => usbInterface.BulkWrite(source.AsSpan(0, count), out bytesWritten, timeout);
}
