namespace UsbDotNet.Descriptor;

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
    List<IUsbEndpointDescriptor> Endpoints { get; }
}
