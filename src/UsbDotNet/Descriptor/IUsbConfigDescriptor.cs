namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents a USB configuration descriptor, providing access to configuration-specific
/// attributes, power requirements, and interface descriptors for a USB device.
/// </summary>
/// <remarks>
/// This interface exposes information about a USB device's configuration as defined by the USB
/// specification. It includes properties for identifying the configuration, describing its
/// characteristics, and enumerating its interfaces and alternate settings. Implementations
/// typically correspond to a single configuration within a USB device, which may support multiple
/// configurations. The interface does not modify device state and is intended for querying
/// descriptor information.
/// </remarks>
public interface IUsbConfigDescriptor
{
    /// <summary>
    /// Identifier value for this configuration.
    /// </summary>
    byte ConfigId { get; }

    /// <summary>
    /// Index of string descriptor describing this configuration.
    /// </summary>
    byte StringDescriptionIndex { get; }

    /// <summary>
    /// Configuration characteristics.
    /// </summary>
    UsbConfigAttributes Attributes { get; }

    /// <summary>
    /// Maximum milliampere power consumption of the USB device from the
    /// bus in this configuration, when the device is fully operation.
    /// </summary>
    int MaxPower { get; }

    /// <summary>
    /// Extra configuration bytes.
    /// </summary>
    byte[] ExtraBytes { get; }

    /// <summary>
    /// A dictionary of USB interface descriptors grouped by interface number. For each interface
    /// number, the value is a dictionary of alternate interface descriptors keyed by the alternate
    /// setting number. Per the USB spec, alternate setting 0 always exists and is the default
    /// alternate setting for each device configuration.
    /// </summary>
    IDictionary<byte, IDictionary<byte, IUsbInterfaceDescriptor>> Interfaces { get; }
}
