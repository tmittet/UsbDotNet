using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UsbDotNet.Descriptor;
using UsbDotNet.DeviceHotplugSample.Device;
using UsbDotNet.Hotplug;

namespace UsbDotNet.DeviceHotplugSample;

/// <summary>
/// Prints a JSON line to stdout each time a device is connected or disconnected, until the host is
/// stopped (Ctrl+C). Devices already connected at startup are printed as "connected". Demonstrates
/// both ways to consume <see cref="IUsbHotplugMonitor"/>: enumerating the subscription directly
/// (default) and the <see cref="UsbHotplugEventNotifier"/> events adapter.
/// </summary>
internal sealed class DeviceHotplugWorker(
    IUsb usb,
    IUsbHotplugMonitor monitor,
    ILoggerFactory loggerFactory,
    IOptions<DeviceHotplugOptions> options
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        usb.Initialize();

        var settings = options.Value;
        var filter = new UsbDeviceFilter(
            VendorIds: settings.VendorId is { } vendorId ? [vendorId] : null,
            ProductIds: settings.ProductId is { } productId ? [productId] : null
        );

        // The error handling is shared on purpose: both modes are the same subscription underneath,
        // so they end in exactly the same ways and neither needs a catch of its own.
        try
        {
            await (
                settings.Mode switch
                {
                    HotplugMode.Stream => RunWithStreamAsync(filter, stoppingToken),
                    HotplugMode.Events => RunWithEventsAsync(filter, stoppingToken),
                }
            );
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on graceful shutdown: our own token ended the subscription.
        }
        catch (OperationCanceledException)
        {
            // The monitor or the underlying IUsb was disposed under us, so undelivered events were
            // dropped and monitoring cannot resume. The absent token is what tells this apart from
            // our own shutdown above.
        }
    }

    /// <summary>Default approach: enumerate the subscription.</summary>
    private async Task RunWithStreamAsync(IUsbDeviceFilter filter, CancellationToken stoppingToken)
    {
        // The subscription starts on the first read: devices already connected then are yielded as
        // Connected events up front, and live events follow without a gap.
        await foreach (var e in monitor.Subscribe(filter, stoppingToken))
        {
            PrintDevice(StateOf(e.Type), e.Descriptor);
        }
    }

    /// <summary>Alternative approach: classic events via the notifier adapter.</summary>
    private async Task RunWithEventsAsync(IUsbDeviceFilter filter, CancellationToken stoppingToken)
    {
        await using var notifier = new UsbHotplugEventNotifier(monitor, filter, loggerFactory);
        // Attach the handlers before RunAsync: it is what subscribes, and the already-connected
        // devices are delivered inside its synchronous prologue.
        notifier.DeviceConnected += (_, e) => PrintDevice("connected", e.Descriptor);
        notifier.DeviceDisconnected += (_, e) => PrintDevice("disconnected", e.Descriptor);
        await notifier.RunAsync(stoppingToken);
    }

    private static string StateOf(UsbHotplugEventType type) =>
        type switch
        {
            UsbHotplugEventType.Connected => "connected",
            UsbHotplugEventType.Disconnected => "disconnected",
        };

    private static void PrintDevice(string state, IUsbDeviceDescriptor d)
    {
        var info = new DeviceInfo(
            state,
            d.DeviceClass,
            $"0x{d.VendorId:X4}",
            $"0x{d.ProductId:X4}",
            d.BusNumber,
            d.BusAddress
        );
        // In stream mode this runs on the reader's task; in events mode on whatever thread the
        // notifier's loop resumed on. Console.WriteLine is thread-safe either way.
        Console.WriteLine(JsonSerializer.Serialize(info, JsonContext.Default.DeviceInfo));
    }
}
