using System.Buffers.Binary;
using UsbDotNet.Core;

namespace UsbDotNet.Extensions.Uvc.Unix;

/// <summary>
/// Internal helpers for UVC control transfers on Linux and macOS:
/// maps cross-platform enum values to UVC spec control selectors and buffer layouts.
/// </summary>
internal static class UvcTransfer
{
    /// <summary>
    /// Returns the UVC control selector, total buffer size in bytes, and byte offset of the signed
    /// integer value within the buffer for a CameraControl (camera terminal control) request.
    /// </summary>
    /// <remarks>
    /// Pan and Tilt share CT_PANTILT_ABSOLUTE_CONTROL (8 bytes).
    /// Pan occupies bytes 0–3, Tilt occupies bytes 4–7.
    /// A read-modify-write is required when setting one axis to preserve the other.
    /// </remarks>
    internal static (
        byte controlSelector,
        int bufferSize,
        int valueOffset
    ) GetCameraControlDescriptor(UvcCameraControl property) =>
        property switch
        {
            UvcCameraControl.Pan => (0x0D, 8, 0), // CT_PANTILT_ABSOLUTE_CONTROL — pan
            UvcCameraControl.Tilt => (0x0D, 8, 4), // CT_PANTILT_ABSOLUTE_CONTROL — tilt
            UvcCameraControl.Roll => (0x0F, 2, 0), // CT_ROLL_ABSOLUTE_CONTROL
            UvcCameraControl.Zoom => (0x0B, 2, 0), // CT_ZOOM_ABSOLUTE_CONTROL
            UvcCameraControl.Exposure => (0x04, 4, 0), // CT_EXPOSURE_TIME_ABSOLUTE_CONTROL
            UvcCameraControl.Iris => (0x09, 2, 0), // CT_IRIS_ABSOLUTE_CONTROL
            UvcCameraControl.Focus => (0x06, 2, 0), // CT_FOCUS_ABSOLUTE_CONTROL
            _ => throw new ArgumentOutOfRangeException(nameof(property), property, null),
        };

    /// <summary>
    /// Returns the UVC control selector and data buffer size in bytes
    /// for an ImageSetting (processing unit) control request.
    /// </summary>
    internal static (byte controlSelector, int bufferSize) GetImageSettingDescriptor(
        UvcImageSetting property
    ) =>
        property switch
        {
            UvcImageSetting.BacklightCompensation => (0x01, 2), // PU_BACKLIGHT_COMPENSATION_CONTROL
            UvcImageSetting.Brightness => (0x02, 2), // PU_BRIGHTNESS_CONTROL
            UvcImageSetting.Contrast => (0x03, 2), // PU_CONTRAST_CONTROL
            UvcImageSetting.Gain => (0x04, 2), // PU_GAIN_CONTROL
            UvcImageSetting.PowerLineFrequency => (0x05, 1), // PU_POWER_LINE_FREQUENCY_CONTROL
            UvcImageSetting.Hue => (0x06, 2), // PU_HUE_CONTROL
            UvcImageSetting.Saturation => (0x07, 2), // PU_SATURATION_CONTROL
            UvcImageSetting.Sharpness => (0x08, 2), // PU_SHARPNESS_CONTROL
            UvcImageSetting.Gamma => (0x09, 2), // PU_GAMMA_CONTROL
            UvcImageSetting.WhiteBalance => (0x0A, 2), // PU_WHITE_BALANCE_TEMPERATURE_CONTROL
            UvcImageSetting.ColorEnable => throw new NotSupportedException(
                $"{nameof(UvcImageSetting.ColorEnable)} has no standard UVC Processing Unit "
                    + "equivalent and is not supported on Linux and macOS."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(property), property, null),
        };

    internal static int ReadInt(byte[] buffer, int offset, int size) =>
        size switch
        {
            4 => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4)),
            2 => BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(offset, 2)),
            1 => (sbyte)buffer[offset],
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };

    internal static void WriteInt(byte[] buffer, int offset, int size, int value)
    {
        switch (size)
        {
            case 4:
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
                break;
            case 2:
                BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(offset, 2), (short)value);
                break;
            case 1:
                buffer[offset] = (byte)(sbyte)value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(size));
        }
    }

    internal static void ThrowIfFailed(UsbResult result, string operation)
    {
        if (result != UsbResult.Success)
            throw new IOException($"UVC {operation} failed: {result}.");
    }
}
