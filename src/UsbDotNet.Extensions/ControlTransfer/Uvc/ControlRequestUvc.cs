namespace UsbDotNet.Extensions.ControlTransfer.Uvc;

/// <summary>
/// USB Video Class (UVC) control request codes. These are used in class-specific
/// control transfers to get/set control values, ranges, and metadata.
/// </summary>
public enum ControlRequestUvc : byte
{
    /// <summary>
    /// Set the current value of a control (Host -> Device)
    /// </summary>
    SetCurrent = 0x01,

    /// <summary>
    /// Set the minimum value of a control (Host -> Device)
    /// </summary>
    SetMinimum = 0x02,

    /// <summary>
    /// Set the maximum value of a control (Host -> Device)
    /// </summary>
    SetMaximum = 0x03,

    /// <summary>
    /// Set the resolution/step-size of a control (Host -> Device)
    /// </summary>
    SetResolution = 0x04,

    /// <summary>
    /// Set the default value of a control (Host -> Device)
    /// </summary>
    SetDefault = 0x07,

    /// <summary>
    /// Set the values of all fields or channels of a control in a single request (Host -> Device).
    /// Introduced in UVC version 1.5.
    /// </summary>
    SetCurrentAll = 0x11,

    /// <summary>
    /// Get the current value of a control (Device -> Host)
    /// </summary>
    GetCurrent = 0x81,

    /// <summary>
    /// Get the minimum supported value of a control (Device -> Host)
    /// </summary>
    GetMinimum = 0x82,

    /// <summary>
    /// Get the maximum supported value of a control (Device -> Host)
    /// </summary>
    GetMaximum = 0x83,

    /// <summary>
    /// Get the resolution/step-size of a control (Device -> Host)
    /// </summary>
    GetResolution = 0x84,

    /// <summary>
    /// Get the length in bytes of the control data (Device -> Host)
    /// </summary>
    GetLength = 0x85,

    /// <summary>
    /// Get information about the control, e.g. support flags (Device -> Host).
    /// Indicates which requests are supported and whether the control is read-only.
    /// </summary>
    GetInfo = 0x86,

    /// <summary>
    /// Get the default value of a control (Device -> Host)
    /// </summary>
    GetDefault = 0x87,

    /// <summary>
    /// Get the values of all fields or channels of a control (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetCurrentAll = 0x91,

    /// <summary>
    /// Get the minimum values of all fields or channels of a control (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetMinimumAll = 0x92,

    /// <summary>
    /// Get the maximum values of all fields or channels of a control (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetMaximumAll = 0x93,

    /// <summary>
    /// Get the resolution/step-size for all fields or channels of a control (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetResolutionAll = 0x94,

    /// <summary>
    /// Get the length in bytes of the control data for all fields or channels (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetLengthAll = 0x95,

    /// <summary>
    /// Get information about a control for all fields or channels (Device -> Host).
    /// Indicates which requests are supported collectively. Introduced in UVC version 1.5.
    /// </summary>
    GetInfoAll = 0x96,

    /// <summary>
    /// Get the default values for all fields or channels of a control (Device -> Host).
    /// Introduced in UVC version 1.5.
    /// </summary>
    GetDefaultAll = 0x97,
}
