namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Flags indicating whether a camera property is set to auto or manual mode.
/// </summary>
/// <remarks>
/// Fully supported on Windows via DirectShow.
/// On Linux and macOS, the GetCameraControl function always returns <see cref="UvcControl.Manual"/>,
/// and the <see cref="UvcControl.Auto"/> flag is ignored by the SetCameraControl function.
/// </remarks>
[Flags]
public enum UvcControl
{
    /// <summary>The property is controlled automatically by the device.</summary>
    Auto = 0x01,

    /// <summary>The property is set manually.</summary>
    Manual = 0x02,
}
