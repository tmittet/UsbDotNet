using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsbDotNet.DeviceHotplugSample;

var mode = ParseMode(GetArg(args, "--mode")); // stream|events
var vendorFilter = TryParseHex(GetArg(args, "--vid")); // e.g. '0x2BD9'
var productFilter = TryParseHex(GetArg(args, "--pid")); // e.g. '0x0021'

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning);
builder
    .Services.Configure<DeviceHotplugOptions>(o =>
    {
        o.Mode = mode;
        o.VendorId = vendorFilter;
        o.ProductId = productFilter;
    })
    .AddUsbDotNet(o => o.NativeLibraryLogLevel = LogLevel.Warning)
    .AddUsbHotplug()
    .AddHostedService<DeviceHotplugWorker>();

using var host = builder.Build();

// Diagnostics go to stderr so stdout stays a clean stream of JSON device events
Console.Error.WriteLine($"Subscribed to hotplug events in '{mode}' mode. Press Ctrl+C to stop.\n");
host.Run();

static string? GetArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static HotplugMode ParseMode(string? value) =>
    string.Equals(value, "events", StringComparison.OrdinalIgnoreCase)
        ? HotplugMode.Events
        : HotplugMode.Stream;

static ushort? TryParseHex(string? value) =>
    string.IsNullOrEmpty(value) ? null
    : ushort.TryParse(
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value.AsSpan(2)
            : value.AsSpan(),
        NumberStyles.HexNumber,
        provider: null,
        out var result
    )
        ? result
    : null;
