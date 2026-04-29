namespace UsbDotNet.LibUsbNative.DeviceListToJsonSample.Device;

[JsonSerializable(typeof(DeviceInfo[]))]
internal sealed partial class SerializationContext : JsonSerializerContext { }
