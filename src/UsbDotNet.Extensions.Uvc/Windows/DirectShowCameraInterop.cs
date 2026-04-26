#nullable disable
// COM interface definitions for DirectShow camera and video processing controls on Windows.
// Nullable is disabled because COM interfaces don't have nullable semantics.

using System.Runtime.InteropServices;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// DirectShow IAMCameraControl COM interface for camera terminal controls
/// (pan, tilt, zoom, exposure, etc.).
/// </summary>
[ComImport]
[Guid("C6E13370-30AC-11d0-A18C-00A0C9118956")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMCameraControl
{
    [PreserveSig]
    int GetRange(
        int property,
        out int min,
        out int max,
        out int steppingDelta,
        out int defaultValue,
        out int capsFlags
    );

    [PreserveSig]
    int Set(int property, int value, int flags);

    [PreserveSig]
    int Get(int property, out int value, out int flags);
}

/// <summary>
/// DirectShow IAMVideoProcAmp COM interface for video processing amplifier controls
/// (brightness, contrast, saturation, gain, etc.).
/// </summary>
[ComImport]
[Guid("C6E13360-30AC-11d0-A18C-00A0C9118956")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAMVideoProcAmp
{
    [PreserveSig]
    int GetRange(
        int property,
        out int min,
        out int max,
        out int steppingDelta,
        out int defaultValue,
        out int capsFlags
    );

    [PreserveSig]
    int Set(int property, int value, int flags);

    [PreserveSig]
    int Get(int property, out int value, out int flags);
}
