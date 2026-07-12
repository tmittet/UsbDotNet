namespace UsbDotNet.Internal;

internal enum HotplugRegistrationResult
{
    /// <summary>Hotplug was registered successfully.</summary>
    Success,

    /// <summary>Hotplug is not supported or unimplemented on this platform.</summary>
    NotSupported,

    /// <summary>Hotplug was already registered on this instance; only one registration is allowed.</summary>
    AlreadyRegistered,
}
