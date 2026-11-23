namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public readonly record struct UsbEndpointDescriptor(
    UsbEndpointAddress EndpointAddress,
    UsbEndpointAttributes Attributes,
    ushort MaxPacketSizeRawValue,
    byte Interval,
    byte Refresh,
    byte SynchAddress,
    ReadOnlyMemory<byte> ExtraBytes
) : IUsbEndpointDescriptor
{
    // Bits 0-10 of wMaxPacketSize are the packet size, bits 11-12 are the number of additional
    // transactions per microframe and bits 13-15 are reserved.
    private const int PacketSizeMask = 0x07FF;
    private const int AdditionalTransactionsShift = 11;
    private const int AdditionalTransactionsMask = 0b11;

    /// <inheritdoc/>
    public readonly ushort MaxPacketSize => (ushort)(MaxPacketSizeRawValue & PacketSizeMask);

    /// <inheritdoc/>
    public readonly byte AdditionalTransactions =>
        (byte)((MaxPacketSizeRawValue >> AdditionalTransactionsShift) & AdditionalTransactionsMask);
}
