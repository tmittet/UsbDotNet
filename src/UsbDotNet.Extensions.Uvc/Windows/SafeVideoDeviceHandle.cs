using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using UsbDotNet.Descriptor;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// A <see cref="SafeHandle"/> wrapping a DirectShow IBaseFilter COM pointer for a Windows
/// UVC (USB Video Class) device. The underlying COM object can be used to query for
/// DirectShow and Kernel Streaming interfaces such as IAMCameraControl, IAMVideoProcAmp,
/// and IKsControl.
/// </summary>
/// <remarks>
/// On Windows, the USB video class driver (usbvideo.sys) takes exclusive ownership of
/// UVC interfaces, preventing direct USB control transfers via libusb. This SafeHandle
/// provides access to the device through the Windows DirectShow / Kernel Streaming API.
/// <para/>
/// The device is located by enumerating the <c>CLSID_VideoInputDeviceCategory</c>
/// DirectShow category and matching the device path against USB VID, PID, and serial
/// number — the same approach used by <c>Huddly/node-uvc</c> on Windows.
/// Serial number matching is always required to safely disambiguate when multiple
/// devices of the same type are connected.
/// <para/>
/// For best results, call <see cref="Open(IUsbDevice, byte)"/> from an STA
/// (Single-Threaded Apartment) thread, as DirectShow components are apartment-threaded.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class SafeVideoDeviceHandle : SafeHandle
{
    private SafeVideoDeviceHandle(IntPtr handle)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    /// <inheritdoc/>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        _ = Marshal.Release(handle);
        return true;
    }

    /// <summary>
    /// Opens a DirectShow video device matching the given USB device.
    /// The device must be open (not disposed) so that the serial number can be read.
    /// </summary>
    /// <param name="device">An open UsbDevice instance — the serial number is read via <see cref="IUsbDevice.GetSerialNumber"/>.</param>
    /// <param name="interfaceNumber">
    /// The UVC VideoControl interface number. Used to match the <c>MI_xx</c> component of the
    /// DirectShow device path on devices with multiple Video Interface Collections.
    /// Devices with only one camera (no <c>MI_xx</c> in their path) are matched regardless.
    /// </param>
    /// <returns>A <see cref="SafeVideoDeviceHandle"/> wrapping the DirectShow IBaseFilter for the matched device.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No matching video device was found or COM initialization failed.</exception>
    public static SafeVideoDeviceHandle Open(IUsbDevice device, byte interfaceNumber)
    {
        ArgumentNullException.ThrowIfNull(device);
        return Open(device.Descriptor, device.GetSerialNumber(), interfaceNumber);
    }

    /// <summary>
    /// Opens a DirectShow video device matching the given USB device descriptor and serial number.
    /// </summary>
    /// <param name="descriptor">The USB device descriptor providing VID and PID for matching.</param>
    /// <param name="serialNumber">
    /// The device serial number, used together with VID/PID to uniquely identify the device.
    /// Required to safely disambiguate when multiple devices of the same VID/PID are connected.
    /// </param>
    /// <param name="interfaceNumber">
    /// The UVC VideoControl interface number. Used to match the <c>MI_xx</c> component of the
    /// DirectShow device path on devices with multiple Video Interface Collections.
    /// Devices with only one camera (no <c>MI_xx</c> in their path) are matched regardless.
    /// </param>
    /// <returns>A <see cref="SafeVideoDeviceHandle"/> wrapping the DirectShow IBaseFilter for the matched device.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> or <paramref name="serialNumber"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="serialNumber"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">No matching video device was found or COM initialization failed.</exception>
    public static SafeVideoDeviceHandle Open(
        IUsbDeviceDescriptor descriptor,
        string serialNumber,
        byte interfaceNumber
    )
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Open(descriptor.VendorId, descriptor.ProductId, serialNumber, interfaceNumber);
    }

    /// <summary>
    /// Opens a DirectShow video device matching the given USB vendor ID, product ID, and serial number.
    /// </summary>
    /// <param name="vendorId">The USB vendor ID (VID) to match.</param>
    /// <param name="productId">The USB product ID (PID) to match.</param>
    /// <param name="serialNumber">
    /// The device serial number, used together with VID/PID to uniquely identify the device.
    /// Required to safely disambiguate when multiple devices of the same VID/PID are connected.
    /// </param>
    /// <param name="interfaceNumber">
    /// The UVC VideoControl interface number. Used to match the <c>MI_xx</c> component of the
    /// DirectShow device path on devices with multiple Video Interface Collections.
    /// Devices with only one camera (no <c>MI_xx</c> in their path) are matched regardless.
    /// </param>
    /// <returns>A <see cref="SafeVideoDeviceHandle"/> wrapping the DirectShow IBaseFilter for the matched device.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serialNumber"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="serialNumber"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">No matching video device was found or COM initialization failed.</exception>
    public static SafeVideoDeviceHandle Open(
        ushort vendorId,
        ushort productId,
        string serialNumber,
        byte interfaceNumber
    )
    {
        ArgumentNullException.ThrowIfNull(serialNumber);
        if (serialNumber.Length == 0)
            throw new ArgumentException("Value cannot be empty.", nameof(serialNumber));
        var devEnumType = Type.GetTypeFromCLSID(
            DirectShowGuids.SystemDeviceEnum,
            throwOnError: true
        )!;
        var devEnum = (ICreateDevEnum)(
            Activator.CreateInstance(devEnumType)
            ?? throw new InvalidOperationException(
                "Failed to create the DirectShow System Device Enumerator."
            )
        );

        try
        {
            var category = DirectShowGuids.VideoInputDeviceCategory;
            var hr = devEnum.CreateClassEnumerator(ref category, out var enumMoniker, 0);

            // S_FALSE (1) means the category is empty.
            if (hr != 0 || enumMoniker is null)
                throw DeviceNotFoundException(vendorId, productId, serialNumber);

            SafeVideoDeviceHandle? result = null;
            try
            {
                while (enumMoniker.Next(1, out var moniker, IntPtr.Zero) == 0)
                {
                    try
                    {
                        result = TryOpenFromMoniker(
                            moniker,
                            vendorId,
                            productId,
                            serialNumber,
                            interfaceNumber
                        );
                        if (result is not null)
                        {
                            var toReturn = result;
                            result = null;
                            return toReturn;
                        }
                    }
                    finally
                    {
                        _ = Marshal.ReleaseComObject(moniker);
                    }
                }
            }
            finally
            {
                result?.Dispose();
                _ = Marshal.ReleaseComObject(enumMoniker);
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(devEnum);
        }

        throw DeviceNotFoundException(vendorId, productId, serialNumber);
    }

    private static SafeVideoDeviceHandle? TryOpenFromMoniker(
        IMoniker moniker,
        ushort vendorId,
        ushort productId,
        string serialNumber,
        byte interfaceNumber
    )
    {
        try
        {
            // Read DevicePath from the moniker's property bag.
            var iidPropertyBag = DirectShowGuids.IPropertyBag;
            moniker.BindToStorage(IntPtr.Zero, null!, ref iidPropertyBag, out var bagObj);
            var propertyBag = (IPropertyBag)bagObj;

            try
            {
                if (
                    propertyBag.Read("DevicePath", out var pathValue, IntPtr.Zero) != 0
                    || pathValue is not string devicePath
                )
                {
                    return null;
                }

                if (
                    !IsMatchingDevice(
                        devicePath,
                        vendorId,
                        productId,
                        serialNumber,
                        interfaceNumber
                    )
                )
                {
                    return null;
                }

                // Match found — bind to the DirectShow filter object.
                var iidBaseFilter = DirectShowGuids.IBaseFilter;
                moniker.BindToObject(IntPtr.Zero, null!, ref iidBaseFilter, out var filterObj);

                // Get a raw IUnknown pointer (calls AddRef).
                var ptr = Marshal.GetIUnknownForObject(filterObj);

                // Release the RCW; our SafeHandle now owns the COM reference.
                _ = Marshal.ReleaseComObject(filterObj);

                return new SafeVideoDeviceHandle(ptr);
            }
            finally
            {
                _ = Marshal.ReleaseComObject(propertyBag);
            }
        }
        catch (COMException)
        {
            // Skip devices that fail to bind (in use, inaccessible, etc.).
            return null;
        }
    }

    /// <summary>
    /// Checks whether a DirectShow device path matches the given USB identifiers.
    /// Device paths follow the pattern: <c>\\?\usb#vid_XXXX&amp;pid_YYYY[&amp;mi_ZZ]#INSTANCE#{device-class-guid}</c>.
    /// The instance ID belongs to the USB interface node, which always uses a Windows-generated
    /// location-based ID (<c>D&amp;...</c>) — the USB serial number lives on the parent composite
    /// device node. When <paramref name="serialNumber"/> is provided, the parent is resolved via
    /// <c>CM_Get_Parent</c> and its device instance ID is parsed to extract the serial.
    /// The <c>MI_ZZ</c> component is only present on devices with multiple Video Interface
    /// Collections; when present it is matched against <paramref name="interfaceNumber"/>.
    /// </summary>
    private static bool IsMatchingDevice(
        string devicePath,
        ushort vendorId,
        ushort productId,
        string serialNumber,
        byte interfaceNumber
    )
    {
        var path = devicePath.ToUpperInvariant();

        // Must be a USB device (filters out software / virtual devices).
        if (!path.Contains("USB#", StringComparison.Ordinal))
            return false;

        if (!path.Contains($"VID_{vendorId:X4}", StringComparison.Ordinal))
            return false;

        if (!path.Contains($"PID_{productId:X4}", StringComparison.Ordinal))
            return false;

        var segments = path.Split('#');
        if (segments.Length < 3)
            return false;

        // The instance ID in segment[2] belongs to the USB interface node and always uses a
        // Windows-generated location-based ID (D&...). The real serial number lives on the
        // parent composite device. Navigate up via CfgMgr32 and compare from there.
        var parentSerial = CfgMgrInterop.GetParentSerialNumber(devicePath);
        if (parentSerial is null)
            return false;
        if (!string.Equals(parentSerial, serialNumber, StringComparison.OrdinalIgnoreCase))
            return false;

        // The first segment contains "VID_XXXX&PID_YYYY" and, on multi-VIC devices, "&MI_ZZ".
        // If MI_ is present, verify it matches the requested interface number.
        // Trim at the next '&' in case there are further components (e.g. &REV_XXXX).
        var hwIdSegment = segments[1];
        var miIndex = hwIdSegment.IndexOf("MI_", StringComparison.Ordinal);
        if (miIndex >= 0)
        {
            var miStart = miIndex + 3;
            var miEnd = hwIdSegment.IndexOf('&', miStart);
            var miValue =
                miEnd >= 0
                    ? hwIdSegment.Substring(miStart, miEnd - miStart)
                    : hwIdSegment.Substring(miStart);
            if (
                !byte.TryParse(
                    miValue,
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out var mi
                )
                || mi != interfaceNumber
            )
            {
                return false;
            }
        }

        return true;
    }

    private static InvalidOperationException DeviceNotFoundException(
        ushort vendorId,
        ushort productId,
        string serialNumber
    ) =>
        new(
            $"No video device found matching VID=0x{vendorId:X4}, PID=0x{productId:X4}, Serial={serialNumber}."
        );
}
