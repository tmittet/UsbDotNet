namespace UsbDotNet.Descriptor;

/// <summary>
/// Provides a read-only descriptor for a USB device, exposing identifying information,
/// configuration details, and connection parameters as defined by the USB specification.
/// </summary>
/// <remarks>
/// This interface supplies properties that represent standard USB device descriptor fields,
/// including vendor and product IDs, device class codes, and connection-specific information such
/// as bus and port numbers. Implementations may use these properties to uniquely identify devices,
/// query their capabilities, or enumerate connected USB devices. The values correspond to those
/// defined in the USB specification and may be used for device matching or configuration purposes.
/// Port and bus numbering may vary between host controllers and is not guaranteed to be stable
/// across sessions or hardware changes.
/// </remarks>
public interface IUsbDeviceDescriptor
{
    /// <summary>
    /// A device connection key that remains unique until the device is disconnected from the host.
    /// </summary>
    string DeviceKey { get; }

    /// <summary>
    /// USB specification release number in binary-coded decimal.
    /// </summary>
    ushort BcdUsb { get; }

    /// <summary>
    /// USB-IF class code for the device.
    /// </summary>
    UsbClass DeviceClass { get; }

    /// <summary>
    /// USB-IF subclass code for the device, qualified by the DeviceClass value.
    /// </summary>
    byte DeviceSubClass { get; }

    /// <summary>
    /// USB-IF protocol code for the device, qualified by the DeviceClass and DeviceSubClass values.
    /// </summary>
    byte DeviceProtocol { get; }

    /// <summary>
    /// Maximum packet size for endpoint 0.
    /// </summary>
    byte MaxPacketSize0 { get; }

    /// <summary>
    /// USB-IF vendor ID.
    /// </summary>
    ushort VendorId { get; }

    /// <summary>
    /// USB-IF product ID.
    /// </summary>
    ushort ProductId { get; }

    /// <summary>
    /// The device release number in binary-coded decimal.
    /// </summary>
    ushort BcdDevice { get; }

    /// <summary>
    /// The index of the manufacturer string descriptor.
    /// </summary>
    byte ManufacturerIndex { get; }

    /// <summary>
    /// The index of the product name string descriptor.
    /// </summary>
    byte ProductIndex { get; }

    /// <summary>
    /// The index of the device serial number string descriptor.
    /// </summary>
    byte SerialNumberIndex { get; }

    /// <summary>
    /// The number of possible configurations.
    /// </summary>
    byte NumConfigurations { get; }

    /// <summary>
    /// The number of the bus that the device is connected to.
    /// </summary>
    byte BusNumber { get; }

    /// <summary>
    /// The address of the device on the bus it's connected to.
    /// </summary>
    byte BusAddress { get; }

    /// <summary>
    /// The number of the port that the device is connected to.
    ///
    /// The number returned by this call is usually guaranteed to be uniquely tied to a physical
    /// port, meaning that different devices plugged on the same physical port should return the
    /// same port number. But there is no guarantee that the port number returned by this call will
    /// remain the same, or even match the order in which ports are numbered on the HUB/HCD.
    /// </summary>
    byte PortNumber { get; }
}
