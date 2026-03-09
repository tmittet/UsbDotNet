namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Provides access to UVC controls on a USB video device, including:
/// <list type="bullet">
/// <item>Camera Terminal controls (pan, tilt, zoom, exposure, iris, focus, roll).</item>
/// <item>Processing Unit controls (brightness, contrast, hue, saturation, gain, etc.).</item>
/// <item>Vendor-defined UVC extension unit controls.</item>
/// </list>
/// </summary>
/// <remarks>
/// On Windows, controls are accessed via DirectShow <c>IAMCameraControl</c>, <c>IAMVideoProcAmp</c> and <c>IKsControl</c>.
/// On Linux and macOS, controls are sent via libusb UVC control transfers using the entity ID.
/// <para/>
/// Auto/manual control types are fully supported on Windows. On Linux and macOS,
/// <see cref="GetImageSetting"/> always returns <see cref="UvcControlType.Manual"/> and
/// the <see cref="UvcControlType.Auto"/> flag is ignored by <see cref="SetImageSetting"/>.
/// <para/>
/// <see cref="UvcImageSetting.ColorEnable"/> is Windows-only; using it on Linux or macOS
/// throws <see cref="NotSupportedException"/>.
/// </remarks>
public interface IUvcControls : IDisposable
{
    /// <summary>Gets the current value and control mode of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to query.</param>
    /// <param name="controlType">The current auto/manual mode (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).</param>
    /// <returns>The current value.</returns>
    int GetCameraControl(UvcCameraControl cameraControl, out UvcControlType controlType);

    /// <summary>Sets the value of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">Auto or manual mode (Windows only; ignored on Linux/macOS). Defaults to <see cref="UvcControlType.Manual"/>.</param>
    void SetCameraControl(
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
    /// <param name="capsFlags">Receives the supported modes (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).</param>
    void GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    );

    /// <summary>Gets the current value and control mode of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to query.</param>
    /// <param name="controlType">The current auto/manual mode (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).</param>
    /// <returns>The current value.</returns>
    int GetImageSetting(UvcImageSetting imageSetting, out UvcControlType controlType);

    /// <summary>Sets the value of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">Auto or manual mode (Windows only; ignored on Linux/macOS). Defaults to <see cref="UvcControlType.Manual"/>.</param>
    void SetImageSetting(
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
    void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    );

    /// <summary>
    /// Reads data from a control within an extension unit;
    /// assuming the device has only one unit/node with the given extension GUID.
    /// </summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes returned by the device.</returns>
    int GetExtensionUnit(Guid extensionGuid, uint control, Span<byte> data);

    /// <summary>Writes data to a control within this extension unit;
    /// assuming the device has only one unit/node with the given extension GUID.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The data to write.</param>
    void SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data);

    /// <summary>
    /// Queries the required data length for a control within this extension unit;
    /// assuming the device has only one unit/node with the given extension GUID.
    /// </summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    int GetExtensionUnitLength(Guid extensionGuid, uint control);

    /// <summary>Reads data from a control within this extension unit.</summary>
    /// <param name="extensionGuid">
    /// The extension unit property set GUID; ignored on Linux and macOS
    /// where the unit is identified by <paramref name="entityId"/> alone.
    /// </param>
    /// <param name="entityId">
    /// The topology node ID on Windows, or the UVC extension unit ID on Linux and macOS.
    /// </param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes returned by the device.</returns>
    int GetExtensionUnit(Guid extensionGuid, uint entityId, uint control, Span<byte> data);

    /// <summary>Writes data to a control within this extension unit.</summary>
    /// <param name="extensionGuid">
    /// The extension unit property set GUID; ignored on Linux and macOS
    /// where the unit is identified by <paramref name="entityId"/> alone.
    /// </param>
    /// <param name="entityId">
    /// The topology node ID on Windows, or the UVC extension unit ID on Linux and macOS.
    /// </param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The data to write.</param>
    void SetExtensionUnit(Guid extensionGuid, uint entityId, uint control, ReadOnlySpan<byte> data);

    /// <summary>
    /// Queries the required data length for a control within this extension unit.
    /// </summary>
    /// <param name="extensionGuid">
    /// The extension unit property set GUID; ignored on Linux and macOS
    /// where the unit is identified by <paramref name="entityId"/> alone.
    /// </param>
    /// <param name="entityId">
    /// The topology node ID on Windows, or the UVC extension unit ID on Linux and macOS.
    /// </param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    int GetExtensionUnitLength(Guid extensionGuid, uint entityId, uint control);
}
