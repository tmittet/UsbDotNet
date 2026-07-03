using System.Text.Json.Serialization;

namespace UsbDotNet.DeviceHotplugSample.Device;

// Source-generated serialization keeps the sample trim- and AOT-safe (no reflection).
[JsonSourceGenerationOptions(WriteIndented = false, UseStringEnumConverter = true)]
[JsonSerializable(typeof(DeviceInfo))]
internal sealed partial class JsonContext : JsonSerializerContext;
