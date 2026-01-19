using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.Extensions;

internal static class native_libusb_descriptor_Extension
{
    internal static libusb_config_descriptor ToPublicConfigDescriptor(
        this native_libusb_config_descriptor descriptor
    ) =>
        new(
            bLength: descriptor.bLength,
            bDescriptorType: (libusb_descriptor_type)descriptor.bDescriptorType,
            wTotalLength: descriptor.wTotalLength,
            bNumInterfaces: descriptor.bNumInterfaces,
            bConfigurationValue: descriptor.bConfigurationValue,
            iConfiguration: descriptor.iConfiguration,
            bmAttributes: (libusb_config_desc_attributes)descriptor.bmAttributes,
            bMaxPower: descriptor.MaxPower,
            interfaces: descriptor
                .ReadInterfaces()
                .Select(a => a.ToPublicInterfaceDescriptor())
                .ToList(),
            extra: ReadBytes(descriptor.extra, descriptor.extra_length)
        );

    internal static libusb_interface ToPublicInterfaceDescriptor(
        this native_libusb_interface descriptor
    ) => new(descriptor.ReadAltSettings().Select(a => a.ToPublicInterfaceDescriptor()).ToList());

    internal static libusb_interface_descriptor ToPublicInterfaceDescriptor(
        this native_libusb_interface_descriptor descriptor
    ) =>
        new(
            bLength: descriptor.bLength,
            bDescriptorType: (libusb_descriptor_type)descriptor.bDescriptorType,
            bInterfaceNumber: descriptor.bInterfaceNumber,
            bAlternateSetting: descriptor.bAlternateSetting,
            bNumEndpoints: descriptor.bNumEndpoints,
            bInterfaceClass: (libusb_class_code)descriptor.bInterfaceClass,
            bInterfaceSubClass: descriptor.bInterfaceSubClass,
            bInterfaceProtocol: descriptor.bInterfaceProtocol,
            iInterface: descriptor.iInterface,
            endpoints: descriptor
                .ReadEndpoints()
                .Select(e => e.ToPublicEndpointDescriptor())
                .ToList(),
            extra: ReadBytes(descriptor.extra, descriptor.extra_length)
        );

    internal static libusb_endpoint_descriptor ToPublicEndpointDescriptor(
        this native_libusb_endpoint_descriptor descriptor
    ) =>
        new(
            bLength: descriptor.bLength,
            bDescriptorType: (libusb_descriptor_type)descriptor.bDescriptorType,
            bEndpointAddress: new libusb_endpoint_address(descriptor.bEndpointAddress),
            bmAttributes: new libusb_endpoint_attributes(descriptor.bmAttributes),
            wMaxPacketSize: descriptor.wMaxPacketSize,
            bInterval: descriptor.bInterval,
            bRefresh: descriptor.bRefresh,
            bSynchAddress: descriptor.bSynchAddress,
            extra: ReadBytes(descriptor.extra, descriptor.extra_length)
        );

    private static byte[] ReadBytes(nint ptr, int length)
    {
        if (ptr == IntPtr.Zero || length <= 0)
        {
            return Array.Empty<byte>();
        }
        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return bytes;
    }
}
