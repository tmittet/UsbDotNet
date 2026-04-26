using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UsbDotNet.Core;

namespace UsbDotNet.DeviceInteractionSample;

internal sealed class DeviceInteraction(
    IUsb usb,
    ILogger<DeviceInteraction> logger,
    IOptions<DeviceInteractionOptions> options
)
{
    public void Run()
    {
        usb.Initialize();

        var devices = usb.GetDeviceList(
            vendorId: options.Value.VendorId,
            productIds: options.Value.ProductId is { } pid ? [pid] : null
        );

        logger.LogInformation("Found {Count} matching USB device(s).", devices.Count);
        foreach (var d in devices)
        {
            logger.LogInformation(
                "VID=0x{VID:X4} PID=0x{PID:X4} Bus={Bus} Addr={Addr}",
                d.VendorId,
                d.ProductId,
                d.BusNumber,
                d.BusAddress
            );
        }

        if (devices.Count == 0)
            return;

        var first = devices.First();
        logger.LogInformation("Opening device '{Key}'...", first.DeviceKey);
        try
        {
            using var device = usb.OpenDevice(first.DeviceKey);
            logger.LogInformation("Manufacturer : {Value}", device.GetManufacturer());
            logger.LogInformation("Product      : {Value}", device.GetProduct());
            logger.LogInformation("Serial       : {Value}", device.GetSerialNumber());
        }
        catch (UsbException ex)
        {
            logger.LogWarning("Failed to open or read from device. {Message}", ex.Message);
        }
    }
}
