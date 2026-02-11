namespace UsbDotNet.Descriptor;

public class UsbEndpointAttributes(byte rawValue)
{
    public UsbEndpointTransferType TransferType { get; } =
        (UsbEndpointTransferType)(rawValue & 0b11);

    /// <summary>
    /// Synchronization type for isochronous endpoints. Bits 2:3 of the RawValue.
    /// </summary>
    public UsbSynchronizationType SyncType { get; } =
        (UsbSynchronizationType)((rawValue >> 2) & 0b11);

    /// <summary>
    /// Usage type for isochronous endpoints. Bits 4:5 of the RawValue.
    /// </summary>
    public UsbIsoUsageType UsageType { get; } = (UsbIsoUsageType)((rawValue >> 4) & 0b11);

    /// <summary>
    /// Raw value of bmAttributes.
    /// </summary>
    public byte RawValue { get; } = rawValue;

    public override string ToString() =>
        $"Transfer={TransferType}, Sync={SyncType}, Usage={UsageType}, Raw=0x{RawValue:X2}";
}
