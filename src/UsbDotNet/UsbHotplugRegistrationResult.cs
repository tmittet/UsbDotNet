namespace UsbDotNet;

/// <summary>
/// The outcome of <see cref="IUsbHotplugProvider.RegisterHotplug"/>.
/// </summary>
public enum UsbHotplugRegistrationResult
{
    /// <summary>Hotplug was registered successfully.</summary>
    Success,

    /// <summary>Hotplug is not supported or unimplemented on this platform.</summary>
    NotSupported,

    /// <summary>Hotplug was already registered on this instance; only one registration is allowed.</summary>
    AlreadyRegistered,
}
