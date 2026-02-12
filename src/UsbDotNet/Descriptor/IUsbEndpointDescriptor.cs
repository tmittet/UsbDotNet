namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents a USB endpoint descriptor, providing information about the endpoint's address,
/// attributes, packet size, polling interval, and additional configuration data as defined by the
/// USB specification.
/// </summary>
/// <remarks>
/// This interface exposes properties corresponding to fields in a USB endpoint descriptor,
/// including support for audio-specific fields such as refresh and synchronization address.
/// Implementations provide access to endpoint configuration details required for USB device
/// communication and setup. Refer to the USB specification for interpretation of individual fields
/// and their valid values.
/// </remarks>
public interface IUsbEndpointDescriptor
{
    /// <summary>
    /// The address of the endpoint described by this descriptor.
    /// </summary>
    UsbEndpointAddress EndpointAddress { get; }

    /// <summary>
    /// Attributes which apply to the endpoint when it is configured using the bConfigurationValue.
    /// </summary>
    UsbEndpointAttributes Attributes { get; }

    /// <summary>
    /// Maximum packet size this endpoint is capable of sending/receiving.
    /// </summary>
    ushort MaxPacketSize { get; }

    /// <summary>
    /// Interval for polling endpoint for data transfers.
    /// </summary>
    byte Interval { get; }

    /// <summary>
    /// For audio devices only: the rate at which synchronization feedback is provided.
    /// </summary>
    byte Refresh { get; }

    /// <summary>
    /// For audio devices only: the address if the synch endpoint.
    /// </summary>
    byte SynchAddress { get; }

    /// <summary>
    /// Extra configuration bytes.
    /// </summary>
    byte[] ExtraBytes { get; }
}
