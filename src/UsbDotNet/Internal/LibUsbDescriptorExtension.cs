using System.Collections.Immutable;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Internal;

internal static class LibUsbDescriptorExtension
{
    internal static IUsbConfigDescriptor ToUsbConfigDescriptor(
        this libusb_config_descriptor descriptor
    ) =>
        new UsbConfigDescriptor(
            ConfigId: descriptor.bConfigurationValue,
            StringDescriptionIndex: descriptor.iConfiguration,
            Attributes: (UsbConfigAttributes)descriptor.bmAttributes,
            MaxPowerRawValue: descriptor.bMaxPower,
            ExtraBytes: descriptor.extra,
            Interfaces: descriptor
                // Flatten the alternate settings, since the index is not useful here
                .interfaces.SelectMany(i => i.altsetting)
                // Group by bInterfaceNumber (not index, which is just the order in the array)
                .GroupBy(a => a.bInterfaceNumber)
                // Create a dictionary keyed on bInterfaceNumber
                .ToDictionary(
                    g => g.Key,
                    // Create a dictionary of alternate settings for each interface
                    g =>
                        (IDictionary<byte, IUsbInterfaceDescriptor>)
                            g.Select(a => a.ToUsbInterfaceDescriptor())
                                .ToDictionary(t => t.AlternateSetting, t => t)
                                .ToImmutableDictionary()
                )
                .ToImmutableDictionary()
        );

    internal static IUsbInterfaceDescriptor ToUsbInterfaceDescriptor(
        this libusb_interface_descriptor descriptor
    ) =>
        new UsbInterfaceDescriptor(
            InterfaceNumber: descriptor.bInterfaceNumber,
            AlternateSetting: descriptor.bAlternateSetting,
            InterfaceClass: (UsbClass)descriptor.bInterfaceClass,
            InterfaceSubClass: descriptor.bInterfaceSubClass,
            InterfaceProtocol: descriptor.bInterfaceProtocol,
            StringDescriptionIndex: descriptor.iInterface,
            ExtraBytes: descriptor.extra,
            Endpoints: [.. descriptor.endpoints.Select(e => e.ToUsbEndpointDescriptor())]
        );

    internal static IUsbEndpointDescriptor ToUsbEndpointDescriptor(
        this libusb_endpoint_descriptor descriptor
    ) =>
        new UsbEndpointDescriptor(
            EndpointAddress: new UsbEndpointAddress(descriptor.bEndpointAddress.RawValue),
            Attributes: new UsbEndpointAttributes(descriptor.bmAttributes.RawValue),
            MaxPacketSize: descriptor.wMaxPacketSize,
            Interval: descriptor.bInterval,
            Refresh: descriptor.bRefresh,
            SynchAddress: descriptor.bSynchAddress,
            ExtraBytes: descriptor.extra
        );
}
