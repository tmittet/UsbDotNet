using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.DeviceListToJsonSample.Device;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

var libusb = new LibUsb();
var context = libusb.CreateContext();
Console.WriteLine($"LibUsb version: {libusb.GetVersion()}\n");
context.RegisterLogCallback((level, message) => Console.WriteLine($"[LibUsb][{level}] {message}"));

using var deviceList = context.GetDeviceList();
Console.WriteLine($"Found {deviceList.Count} USB devices:");
Console.WriteLine(
    JsonSerializer.Serialize(
        deviceList.Select(d => GetDeviceInfo(d, d.GetDeviceDescriptor())),
        GetJsonSerializerOptions()
    )
);

DeviceInfo GetDeviceInfo(ISafeDevice device, libusb_device_descriptor descriptor) =>
    new(
        descriptor,
        device.GetBusNumber(),
        device.GetDeviceAddress(),
        device.GetPortNumber(),
        [.. ReadStringDescriptors(device, descriptor)],
        [
            .. Enumerable
                .Range(0, descriptor.bNumConfigurations)
                .Select(i => device.GetConfigDescriptor((byte)i)),
        ]
    );

IEnumerable<DeviceStringDescriptor> ReadStringDescriptors(
    ISafeDevice device,
    libusb_device_descriptor descriptor
)
{
    using var handle = TryOpenDevice(device);
    if (handle is null)
        yield break;
    if (handle.TryGetStringDescriptorAscii(descriptor.iManufacturer, out var manufacturer, out _))
        yield return new(descriptor.iManufacturer, "Manufacturer", manufacturer);
    if (handle.TryGetStringDescriptorAscii(descriptor.iProduct, out var product, out _))
        yield return new(descriptor.iProduct, "Product", product);
    if (handle.TryGetStringDescriptorAscii(descriptor.iSerialNumber, out var serialNumber, out _))
        yield return new(descriptor.iSerialNumber, "SerialNumber", serialNumber);
}

static ISafeDeviceHandle? TryOpenDevice(ISafeDevice device)
{
    try
    {
        return device.Open();
    }
    catch (Exception ex)
    {
        Console.WriteLine("[WARN] {0}", ex.Message);
        return null;
    }
}

static JsonSerializerOptions GetJsonSerializerOptions() =>
    new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            SerializationContext.Default,
            LibUsbSerializationContext.Default
        ),
    };
