namespace UsbDotNet.DeviceHotplugSample.Device;

internal sealed record DeviceInfo(
    string State,
    UsbClass DeviceClass,
    string VendorId,
    string ProductId,
    byte BusNumber,
    byte BusAddress
);
