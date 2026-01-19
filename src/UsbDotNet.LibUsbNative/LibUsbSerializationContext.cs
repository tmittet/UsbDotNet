using System.Text.Json.Serialization;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
)]
[JsonSerializable(typeof(libusb_config_descriptor))]
[JsonSerializable(typeof(libusb_config_descriptor[]))]
[JsonSerializable(typeof(IReadOnlyList<libusb_config_descriptor>))]
[JsonSerializable(typeof(libusb_device_descriptor))]
[JsonSerializable(typeof(libusb_endpoint_address))]
[JsonSerializable(typeof(libusb_endpoint_attributes))]
[JsonSerializable(typeof(libusb_endpoint_descriptor))]
[JsonSerializable(typeof(libusb_endpoint_descriptor[]))]
[JsonSerializable(typeof(IReadOnlyList<libusb_endpoint_descriptor>))]
[JsonSerializable(typeof(libusb_interface))]
[JsonSerializable(typeof(libusb_interface[]))]
[JsonSerializable(typeof(IReadOnlyList<libusb_interface>))]
[JsonSerializable(typeof(libusb_interface_descriptor))]
[JsonSerializable(typeof(libusb_interface_descriptor[]))]
[JsonSerializable(typeof(IReadOnlyList<libusb_interface_descriptor>))]
[JsonSerializable(typeof(libusb_version))]
public partial class LibUsbSerializationContext : JsonSerializerContext { }
