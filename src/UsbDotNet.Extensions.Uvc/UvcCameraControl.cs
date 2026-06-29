namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Camera terminal control properties (KSPROPERTY_CAMERACONTROL_*) on Windows;
/// mapped to UVC camera terminal control selectors on Linux and macOS.
/// </summary>
public enum UvcCameraControl
{
    /// <summary>Horizontal rotation of the camera in arc-second units (UVC control 0x0D).</summary>
    Pan = 0x00,

    /// <summary>Vertical rotation of the camera in arc-second units (UVC control 0x0D).</summary>
    Tilt = 0x01,

    /// <summary>Rotation around the viewing axis in degree units (UVC control 0x0F).</summary>
    Roll = 0x02,

    /// <summary>Focal length of the lens in millimeter units (UVC control 0x0B).</summary>
    Zoom = 0x03,

    /// <summary>Exposure time in log2 seconds (e.g. -5 = 1/32s) (UVC control 0x04).</summary>
    Exposure = 0x04,

    /// <summary>Aperture setting in fStop * 10 units (UVC control 0x09).</summary>
    Iris = 0x05,

    /// <summary>Focus distance in millimeter units (UVC control 0x06).</summary>
    Focus = 0x06,
}
