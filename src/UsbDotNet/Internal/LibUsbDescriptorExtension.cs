using System.Collections.Immutable;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Internal;

internal static class LibUsbDescriptorExtension
{
    /// <summary>
    /// The first USB specification release with SuperSpeed, as a bcdUSB value.
    /// </summary>
    private const ushort BcdUsb3 = 0x0300;

    /// <summary>
    /// The bMaxPacketSize0 of a device operating at SuperSpeed or faster; an exponent, 2^9 = 512.
    /// At lower speeds the field is the size in bytes: 8, 16, 32 or 64.
    /// </summary>
    private const byte SuperSpeedMaxPacketSize0 = 9;

    /// <summary>
    /// Convert a libusb config descriptor of a device operating at the given speed.
    /// </summary>
    /// <param name="descriptor">The libusb config descriptor.</param>
    /// <param name="speed">The speed the device operates at.</param>
    /// <param name="deviceDescriptor">
    /// The device descriptor of the device; used when the operating speed is unknown.
    /// </param>
    internal static IUsbConfigDescriptor ToUsbConfigDescriptor(
        this libusb_config_descriptor descriptor,
        libusb_speed speed,
        IUsbDeviceDescriptor deviceDescriptor
    ) =>
        new UsbConfigDescriptor(
            ConfigId: descriptor.bConfigurationValue,
            StringDescriptionIndex: descriptor.iConfiguration,
            Attributes: (UsbConfigAttributes)descriptor.bmAttributes,
            MaxPowerRawValue: descriptor.bMaxPower,
            MaxPowerMultiplier: GetMaxPowerMultiplier(speed, deviceDescriptor),
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

    /// <summary>
    /// Get the multiplier that converts bMaxPower to milliampere: 8 for a device operating at
    /// SuperSpeed or faster, otherwise 2. It follows the operating speed, not what the device is
    /// capable of; a SuperSpeed device on a high speed port reports in units of 2 mA.
    /// </summary>
    internal static int GetMaxPowerMultiplier(
        libusb_speed speed,
        IUsbDeviceDescriptor deviceDescriptor
    )
    {
        // By the USB specification bcdUSB alone would do: a device reports 3.0 or higher only
        // when it operates at SuperSpeed or faster, and 2.1 when connected at a lower speed.
        // The negotiated speed is preferred since it is also right for a device that reports
        // the same bcdUSB at every speed, and reading it is free; libusb caches the speed when
        // the device is enumerated. The device descriptor is the fallback, since not every OS
        // reports the speed. It must also have the SuperSpeed bMaxPacketSize0, which a device
        // that reports the same bcdUSB at every speed does not have at a lower speed.
        var isSuperSpeed =
            speed is libusb_speed.LIBUSB_SPEED_UNKNOWN
                ? deviceDescriptor.BcdUsb >= BcdUsb3
                    && deviceDescriptor.MaxPacketSize0 == SuperSpeedMaxPacketSize0
                // Not a switch; a speed added in a later libusb version is also SuperSpeed or faster
                : speed >= libusb_speed.LIBUSB_SPEED_SUPER;
        return isSuperSpeed ? 8 : 2;
    }

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
            MaxPacketSizeRawValue: descriptor.wMaxPacketSize,
            Interval: descriptor.bInterval,
            Refresh: descriptor.bRefresh,
            SynchAddress: descriptor.bSynchAddress,
            ExtraBytes: descriptor.extra
        );
}
