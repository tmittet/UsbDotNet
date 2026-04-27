using UsbDotNet.Core;

namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Provides access to UVC controls on a USB video device, including:
/// <list type="bullet">
/// <item>Camera Terminal controls (pan, tilt, zoom, exposure, iris, focus, roll).</item>
/// <item>Processing Unit controls (brightness, contrast, hue, saturation, gain, etc.).</item>
/// <item>Vendor specific UVC extension unit controls.</item>
/// </list>
/// </summary>
/// <remarks>
/// On Linux and macOS, controls are sent via UsbDotNet UVC control transfers.
/// On Windows, controls are accessed via DirectShow
/// <c>IAMCameraControl</c>, <c>IAMVideoProcAmp</c> and <c>IKsControl</c>.
/// <para/>
/// Auto/manual control types are fully supported on Windows. On Linux and macOS,
/// <see cref="GetImageSetting"/> always returns <see cref="UvcControlType.Manual"/> and
/// the <see cref="UvcControlType.Auto"/> flag is ignored by <see cref="SetImageSetting"/>.
/// <para/>
/// <see cref="UvcImageSetting.ColorEnable"/> is Windows-only; using it on Linux or macOS
/// returns <see cref="UsbResult.NotSupported"/>.
/// <para/>
/// All methods return <see cref="UsbResult"/> to indicate success or failure.
/// On failure, output parameters are set to their default values.
/// <para/>
/// All methods throw <see cref="ObjectDisposedException"/> if called after disposal.
/// </remarks>
public interface IUvcControl : IDisposable
{
    /// <summary>Gets the current value and control mode of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to query.</param>
    /// <param name="value">Receives the current value.</param>
    /// <param name="controlType">
    /// Receives the current auto/manual mode
    /// (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).
    /// </param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the control was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetCameraControl(
        UvcCameraControl cameraControl,
        out int value,
        out UvcControlType controlType
    );

    /// <summary>Sets the value of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">Auto or manual mode (Windows only; ignored on Linux/macOS).</param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the control was set successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    );

    /// <summary>Gets the supported range and capabilities of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to query.</param>
    /// <param name="minValue">Receives the minimum supported value.</param>
    /// <param name="maxValue">Receives the maximum supported value.</param>
    /// <param name="stepSize">Receives the stepping delta between valid values.</param>
    /// <param name="defaultValue">Receives the default value.</param>
    /// <param name="capsFlags">
    /// Receives the supported modes
    /// (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).
    /// </param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the range was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    );

    /// <summary>Gets the current value and control mode of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to query.</param>
    /// <param name="value">Receives the current value.</param>
    /// <param name="controlType">
    /// Receives the current auto/manual mode
    /// (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).
    /// </param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the setting was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the setting is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetImageSetting(
        UvcImageSetting imageSetting,
        out int value,
        out UvcControlType controlType
    );

    /// <summary>Sets the value of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">
    /// Auto or manual mode (Windows only; ignored on Linux/macOS).
    /// </param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the setting was set successfully;
    /// <see cref="UsbResult.NotSupported"/> if the setting is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    );

    /// <summary>Gets the supported range and capabilities of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to query.</param>
    /// <param name="minValue">Receives the minimum supported value.</param>
    /// <param name="maxValue">Receives the maximum supported value.</param>
    /// <param name="stepSize">Receives the stepping delta between valid values.</param>
    /// <param name="defaultValue">Receives the default value.</param>
    /// <param name="capsFlags">Receives the supported modes (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).</param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the range was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the setting is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    );

    /// <summary>Queries the required data length for a control within this extension unit.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">The control selector within the extension unit.</param>
    /// <param name="length">Receives the required buffer length in bytes.</param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the length was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetExtensionUnitLength(Guid extensionGuid, uint control, out int length);

    /// <summary>Reads data from a control within an extension unit.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">The control selector within the extension unit.</param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <param name="bytesRead">Receives the number of bytes returned by the device.</param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the data was read successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult GetExtensionUnit(
        Guid extensionGuid,
        uint control,
        Span<byte> data,
        out int bytesRead
    );

    /// <summary>Writes data to a control within this extension unit.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">The control selector within the extension unit.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>
    /// <see cref="UsbResult.Success"/> if the data was written successfully;
    /// <see cref="UsbResult.NotSupported"/> if the control is not supported by the device or
    /// platform; otherwise an error code describing the failure.
    /// </returns>
    UsbResult SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data);
}
