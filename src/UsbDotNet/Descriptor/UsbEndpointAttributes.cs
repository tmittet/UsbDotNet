namespace UsbDotNet.Descriptor;

/// <summary>
/// Represents the attributes of a USB endpoint, including
/// transfer type, synchronization type, and usage type.
/// Two attributes are equal when their raw values are equal.
/// </summary>
/// <param name="RawValue">Raw value of bmAttributes.</param>
public readonly record struct UsbEndpointAttributes(byte RawValue)
{
    /// <summary>
    /// Gets the USB endpoint transfer type associated with this endpoint.
    /// </summary>
    public UsbEndpointTransferType TransferType => (UsbEndpointTransferType)(RawValue & 0b11);

    /// <summary>
    /// Synchronization type for isochronous endpoints. Bits 2:3 of the RawValue.
    /// </summary>
    public UsbSynchronizationType SyncType => (UsbSynchronizationType)((RawValue >> 2) & 0b11);

    /// <summary>
    /// Usage type for isochronous endpoints. Bits 4:5 of the RawValue.
    /// </summary>
    public UsbIsoUsageType UsageType => (UsbIsoUsageType)((RawValue >> 4) & 0b11);

    /// <inheritdoc/>
    public override string ToString() =>
        $"Transfer={TransferType}, Sync={SyncType}, Usage={UsageType}, Raw=0x{RawValue:X2}";
}
