using System.Text.Json;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.Extensions;

public static class libusb_descriptor_ToJsonExtension
{
    public static string ToJson(this libusb_device_descriptor deviceDescriptor) =>
        JsonSerializer.Serialize(
            deviceDescriptor,
            LibUsbSerializationContext.Default.libusb_device_descriptor
        );

    public static string ToJson(this libusb_config_descriptor configDescriptor) =>
        JsonSerializer.Serialize(
            configDescriptor,
            LibUsbSerializationContext.Default.libusb_config_descriptor
        );

    public static string ToJson(this libusb_interface usbInterface) =>
        JsonSerializer.Serialize(usbInterface, LibUsbSerializationContext.Default.libusb_interface);

    public static string ToJson(this libusb_interface_descriptor interfaceDescriptor) =>
        JsonSerializer.Serialize(
            interfaceDescriptor,
            LibUsbSerializationContext.Default.libusb_interface_descriptor
        );

    public static string ToJson(this libusb_endpoint_descriptor endpointDescriptor) =>
        JsonSerializer.Serialize(
            endpointDescriptor,
            LibUsbSerializationContext.Default.libusb_endpoint_descriptor
        );
}
