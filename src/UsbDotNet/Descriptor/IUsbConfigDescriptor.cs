namespace UsbDotNet.Descriptor;

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
