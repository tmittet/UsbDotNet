namespace UsbDotNet.Descriptor;

public class UsbEndpointAttributes
{
    public UsbEndpointTransferType TransferType { get; }

    /// <summary>
    /// Synchronization type for isochronous endpoints. Bits 2:3 of the RawValue.
    /// </summary>
    public UsbSynchronizationType SyncType { get; }

    /// <summary>
    /// Usage type for isochronous endpoints. Bits 4:5 of the RawValue.
    /// </summary>
    public UsbIsoUsageType UsageType { get; }

    /// <summary>
    /// Raw value of bmAttributes.
    /// </summary>
    public byte RawValue { get; }

    public UsbEndpointAttributes(byte rawValue)
    {
        RawValue = rawValue;
        TransferType = (UsbEndpointTransferType)(rawValue & 0b11);
        SyncType = (UsbSynchronizationType)((rawValue >> 2) & 0b11);
        UsageType = (UsbIsoUsageType)((rawValue >> 4) & 0b11);
    }

    public override string ToString() =>
        $"Transfer={TransferType}, Sync={SyncType}, Usage={UsageType}, Raw=0x{RawValue:X2}";
}
