namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents a USB interface descriptor, providing information about
/// a specific interface within a USB device configuration.
/// </summary>
/// <remarks>
/// This interface exposes properties that correspond to the standard USB interface descriptor
/// fields, including interface number, alternate setting, class, subclass, protocol, and associated
/// endpoints. Implementations typically reflect the structure defined by the USB specification. Use
/// this interface to query interface-level metadata and endpoint information when enumerating or
/// interacting with USB devices.
/// </remarks>
public interface IUsbInterfaceDescriptor
{
    /// <summary>
    /// Number of this interface.
    /// </summary>
    byte InterfaceNumber { get; }

    /// <summary>
    /// Value used to select this alternate setting for this interface.
    /// AlternateSetting == 0 is the default setting for the active configuration.
    /// </summary>
    byte AlternateSetting { get; }

    /// <summary>
    /// USB-IF class code for this interface.
    /// </summary>
    UsbClass InterfaceClass { get; }

    /// <summary>
    /// USB-IF subclass code for this interface, qualified by the bInterfaceClass value.
    /// </summary>
    byte InterfaceSubClass { get; }

    /// <summary>
    /// USB-IF protocol code for this interface, qualified by the bInterfaceClass and bInterfaceSubClass values
    /// </summary>
    byte InterfaceProtocol { get; }

    /// <summary>
    /// Index of string descriptor describing this interface.
    /// </summary>
    byte StringDescriptionIndex { get; }

    /// <summary>
    /// Extra configuration bytes.
    /// </summary>
    byte[] ExtraBytes { get; }

    /// <summary>
    /// A list of endpoints.
    /// </summary>
    IReadOnlyCollection<IUsbEndpointDescriptor> Endpoints { get; }
}
