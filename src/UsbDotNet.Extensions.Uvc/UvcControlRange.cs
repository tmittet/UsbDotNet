namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// The supported range and capabilities of a UVC control.
/// </summary>
/// <param name="Min">The minimum supported value.</param>
/// <param name="Max">The maximum supported value.</param>
/// <param name="Step">
/// The stepping delta between valid values. A step of 0 means that the control does not support
/// stepping.
/// </param>
/// <param name="Default">The default value.</param>
/// <param name="Capabilities">
/// The supported modes (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).
/// </param>
public readonly record struct UvcControlRange(
    int Min,
    int Max,
    int Step,
    int Default,
    UvcControlType Capabilities
);
