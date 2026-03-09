// COM interface definitions for DirectShow device enumeration on Windows.

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// DirectShow and related COM GUIDs used for video device enumeration.
/// </summary>
internal static class DirectShowGuids
{
    /// <summary>CLSID for the System Device Enumerator.</summary>
    public static readonly Guid SystemDeviceEnum = new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");

    /// <summary>CLSID for the Video Input Device category.</summary>
    public static readonly Guid VideoInputDeviceCategory = new(
        "860BB310-5D01-11d0-BD3B-00A0C911CE86"
    );

    /// <summary>IID for IBaseFilter.</summary>
    public static readonly Guid IBaseFilter = new("56a86895-0ad4-11ce-b03a-0020af0ba770");

    /// <summary>IID for IPropertyBag.</summary>
    public static readonly Guid IPropertyBag = new("55272A00-42CB-11CE-8135-00AA004BB851");
}
