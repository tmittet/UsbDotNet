namespace UsbDotNet.DeviceHotplugSample;

internal sealed class DeviceHotplugOptions
{
    public HotplugMode Mode { get; set; } = HotplugMode.Stream;
    public ushort? VendorId { get; set; }
    public ushort? ProductId { get; set; }
}
