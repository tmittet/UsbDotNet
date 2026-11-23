using UsbDotNet.Core;

namespace UsbDotNet;

/// <summary>
/// Extension methods for IUsbInterface.
/// </summary>
public static class UsbInterfaceExtension
{
    /// <summary>
    /// Performs a bulk write operation on the specified USB interface, writing data from the
    /// provided byte array.
    /// <para>
    /// NOTE: Consider using the Span-based BulkWrite method for improved performance and reduced
    /// memory allocations.
    /// </para>
    /// </summary>
    public static UsbResult BulkWrite(
        this IUsbInterface usbInterface,
        byte[] source,
        int count,
        out int bytesWritten,
        int timeout = Timeout.Infinite
    ) => usbInterface.BulkWrite(source.AsSpan(0, count), out bytesWritten, timeout);
}
