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
/// both ways to consume <see cref="IUsbHotplugMonitor"/>: reading a subscription channel (default)
/// and the classic <see cref="UsbHotplugEventNotifier"/> events adapter.
/// </summary>
internal sealed class DeviceHotplugWorker(
    IUsb usb,
    IUsbHotplugMonitor monitor,
    ILoggerFactory loggerFactory,
    IOptions<DeviceHotplugOptions> options
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        usb.Initialize();

        var settings = options.Value;
        var filter = new UsbDeviceFilter(
            VendorId: settings.VendorId,
            ProductIds: settings.ProductId is { } productId ? [productId] : null
        );

        return settings.Mode switch
        {
            HotplugMode.Channels => RunWithChannelsAsync(filter, stoppingToken),
            HotplugMode.Events => RunWithEventsAsync(filter, stoppingToken),
        };
    }

    /// <summary>Default approach: read events straight from the subscription channel.</summary>
    private async Task RunWithChannelsAsync(UsbDeviceFilter filter, CancellationToken stoppingToken)
    {
        using var subscription = monitor.Subscribe(filter);
        try
        {
            await foreach (
                var e in subscription.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false)
            )
            {
                PrintDevice(StateOf(e.Type), e.Descriptor);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
    }

    /// <summary>Alternative approach: classic events via the notifier adapter.</summary>
    private async Task RunWithEventsAsync(UsbDeviceFilter filter, CancellationToken stoppingToken)
    {
        using var notifier = new UsbHotplugEventNotifier(monitor, filter, loggerFactory);
        // Attach handlers before Start() so the initial snapshot of connected devices is delivered.
        notifier.DeviceConnected += (_, e) => PrintDevice("connected", e.Descriptor);
        notifier.DeviceDisconnected += (_, e) => PrintDevice("disconnected", e.Descriptor);
        notifier.Start();
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
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
        // In channels mode this runs on the reader task; in events mode on the notifier's pump
        // thread. Console.WriteLine is thread-safe either way.
        Console.WriteLine(JsonSerializer.Serialize(info, JsonContext.Default.DeviceInfo));
    }
}
