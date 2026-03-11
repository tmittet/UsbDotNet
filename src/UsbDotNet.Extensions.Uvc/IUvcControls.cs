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
/// throws <see cref="NotSupportedException"/>.
/// </remarks>
public interface IUvcControls : IDisposable
{
    /// <summary>Gets the current value and control mode of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to query.</param>
    /// <param name="controlType">The current auto/manual mode (Windows only; always <see cref="UvcControlType.Manual"/> on Linux/macOS).</param>
    /// <returns>The current value.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    int GetCameraControl(UvcCameraControl cameraControl, out UvcControlType controlType);

    /// <summary>Sets the value of a Camera Terminal control.</summary>
    /// <param name="cameraControl">The camera control to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">Auto or manual mode (Windows only; ignored on Linux/macOS). Defaults to <see cref="UvcControlType.Manual"/>.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
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
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
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
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the setting is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    int GetImageSetting(UvcImageSetting imageSetting, out UvcControlType controlType);

    /// <summary>Sets the value of a processing unit image setting.</summary>
    /// <param name="imageSetting">The image setting to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">Auto or manual mode (Windows only; ignored on Linux/macOS). Defaults to <see cref="UvcControlType.Manual"/>.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the setting is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
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
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the setting is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    );

    /// <summary>Queries the required data length for a control within this extension unit</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    int GetExtensionUnitLength(Guid extensionGuid, uint control);

    /// <summary>Reads data from a control within an extension unit.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes returned by the device.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    int GetExtensionUnit(Guid extensionGuid, uint control, Span<byte> data);

    /// <summary>Writes data to a control within this extension unit.</summary>
    /// <param name="extensionGuid">The extension unit property set GUID.</param>
    /// <param name="control">
    /// The control selector within the extension unit.
    /// </param>
    /// <param name="data">The data to write.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when any method is called after the instance has been disposed.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the control is not supported by the device or platform.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when a control transfer fails on Linux or macOS or
    /// an exception is thrown by the underlying API implementation on Windows.
    /// </exception>
    void SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data);
}
