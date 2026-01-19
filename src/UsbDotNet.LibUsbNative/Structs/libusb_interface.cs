using System.Text.Json.Serialization;

namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// A collection of alternate settings for a particular USB interface.
/// </summary>
public readonly record struct libusb_interface
{
    /// <summary>
    /// An array of interface descriptors for each supported alternate setting. There will always be
    /// at least one libusb_interface_descriptor in the array. The active alternate setting may be
    /// set by calling libusb_set_interface_alt_setting.
    ///
    /// NOTE: The array index of the libusb_interface_descriptor within the altsetting array may not
    /// correspond to the value of libusb_interface_descriptor.bAlternateSetting and it should not
    /// be assumed that index zero is the default altsetting, default is bAlternateSetting == 0.
    /// </summary>
    public IReadOnlyList<libusb_interface_descriptor> altsetting { get; }

    [JsonConstructor]
    public libusb_interface(IReadOnlyList<libusb_interface_descriptor> altsetting)
    {
        this.altsetting = altsetting;
    }
}
