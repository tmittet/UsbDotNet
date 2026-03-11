namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Video processing amplifier properties (KSPROPERTY_VIDEOPROCAMP_*) on Windows;
/// mapped to UVC processing unit control selectors on Linux and macOS.
/// </summary>
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
public enum UvcImageSetting
{
    /// <summary>Brightness level (UVC control 0x02).</summary>
    Brightness = 0x00,

    /// <summary>Contrast level (UVC control 0x03).</summary>
    Contrast = 0x01,

    /// <summary>Hue setting in degrees (UVC control 0x06).</summary>
    Hue = 0x02,

    /// <summary>Saturation level (UVC control 0x07).</summary>
    Saturation = 0x03,

    /// <summary>Sharpness level (UVC control 0x08).</summary>
    Sharpness = 0x04,

    /// <summary>Gamma correction (UVC control 0x09).</summary>
    Gamma = 0x05,

    /// <summary>Color enable toggle. (UVC control not supported).</summary>
    ColorEnable = 0x06,

    /// <summary>White balance temperature in Kelvin (UVC control 0x0A).</summary>
    WhiteBalance = 0x07,

    /// <summary>Backlight compensation level (UVC control 0x01).</summary>
    BacklightCompensation = 0x08,

    /// <summary>Gain in arbitrary units (UVC control 0x04).</summary>
    Gain = 0x09,

    /// <summary>
    /// Power line frequency for anti-flicker; 0=disabled, 1=50Hz, 2=60Hz  (UVC control 0x05).
    /// </summary>
    PowerLineFrequency = 0x013,
}
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
