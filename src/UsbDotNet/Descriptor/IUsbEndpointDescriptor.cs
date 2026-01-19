namespace UsbDotNet.Descriptor;

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
