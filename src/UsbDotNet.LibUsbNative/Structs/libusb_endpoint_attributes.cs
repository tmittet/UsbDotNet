using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// Strongly typed view of endpoint libusb_endpoint_descriptor.bmAttributes.
/// </summary>
public readonly record struct libusb_endpoint_attributes
{
    public libusb_endpoint_transfer_type TransferType { get; }

    /// <summary>
    /// Synchronization type for isochronous endpoints. Bits 2:3 of the rawValue.
    /// </summary>
    public libusb_iso_sync_type SyncType { get; }

    /// <summary>
    /// Usage type for isochronous endpoints. Bits 4:5 of the rawValue.
    /// </summary>
    public libusb_iso_usage_type UsageType { get; }

    /// <summary>
    /// Raw value of bmAttributes.
    /// </summary>
    public byte RawValue { get; }

    [JsonConstructor]
    public libusb_endpoint_attributes(byte rawValue)
    {
        RawValue = rawValue;
        TransferType = (libusb_endpoint_transfer_type)(rawValue & 0b11);
        SyncType = (libusb_iso_sync_type)((rawValue >> 2) & 0b11);
        UsageType = (libusb_iso_usage_type)((rawValue >> 4) & 0b11);
    }

    public override string ToString() =>
        $"Transfer={TransferType}, Sync={SyncType}, Usage={UsageType}, Raw=0x{RawValue:X2}";
}
