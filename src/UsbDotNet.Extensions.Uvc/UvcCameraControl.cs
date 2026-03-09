namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// UVC camera terminal control selectors on Linux and macOS;
/// mapped tp camera terminal control properties (KSPROPERTY_CAMERACONTROL_*) on Windows.
/// </summary>
public enum UvcCameraControl
{
    /// <summary>Horizontal rotation of the camera in arc-second units.</summary>
    Pan = 0x00,

    /// <summary>Vertical rotation of the camera in arc-second units.</summary>
    Tilt = 0x01,

    /// <summary>Rotation around the viewing axis in degree units.</summary>
    Roll = 0x02,

    /// <summary>Focal length of the lens in millimeter units.</summary>
    Zoom = 0x03,

    /// <summary>Exposure time in log2 seconds (e.g. -5 = 1/32s).</summary>
    Exposure = 0x04,

    /// <summary>Aperture setting in fStop * 10 units.</summary>
    Iris = 0x05,

    /// <summary>Focus distance in millimeter units.</summary>
    Focus = 0x06,
}
