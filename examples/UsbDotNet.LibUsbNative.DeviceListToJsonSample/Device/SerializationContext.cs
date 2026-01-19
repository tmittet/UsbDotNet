namespace UsbDotNet.LibUsbNative.DeviceListToJsonSample.Device;

[JsonSerializable(typeof(IEnumerable<DeviceInfo>))]
[JsonSerializable(typeof(DeviceInfo[]))]
internal sealed partial class SerializationContext : JsonSerializerContext { }
