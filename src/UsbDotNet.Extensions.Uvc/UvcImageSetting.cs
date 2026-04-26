namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Video processing amplifier properties (KSPROPERTY_VIDEOPROCAMP_* on Windows;
/// UVC Processing Unit control selectors on Linux and macOS).
/// Values are non-contiguous because they match the Windows SDK constants.
/// </summary>
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
public enum UvcImageSetting
{
    /// <summary>Brightness level.</summary>
    Brightness = 0x00,

    /// <summary>Contrast level.</summary>
    Contrast = 0x01,

    /// <summary>Hue setting in degrees.</summary>
    Hue = 0x02,

    /// <summary>Saturation level.</summary>
    Saturation = 0x03,

    /// <summary>Sharpness level.</summary>
    Sharpness = 0x04,

    /// <summary>Gamma correction.</summary>
    Gamma = 0x05,

    /// <summary>Color enable toggle. Not supported via UVC control transfers on Linux and macOS.</summary>
    ColorEnable = 0x06,

    /// <summary>White balance temperature in Kelvin.</summary>
    WhiteBalance = 0x07,

    /// <summary>Backlight compensation level.</summary>
    BacklightCompensation = 0x08,

    /// <summary>Gain in arbitrary units.</summary>
    Gain = 0x09,

    /// <summary>Power line frequency for anti-flicker (0=disabled, 1=50Hz, 2=60Hz).</summary>
    PowerLineFrequency = 0x013,
}
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
