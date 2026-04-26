using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsbDotNet.DeviceInteractionSample;

// Optional CLI args: --vid 0x2BD9 --pid 0x0021
var vendorFilter = TryParseHex(GetArg(args, "--vid"));
var productFilter = TryParseHex(GetArg(args, "--pid"));

var builder = Host.CreateApplicationBuilder(args);
builder
    .Logging.AddSimpleConsole()
    .Services.Configure<DeviceInteractionOptions>(o =>
    {
        o.VendorId = vendorFilter;
        o.ProductId = productFilter;
    })
    .AddUsbDotNet(o => o.NativeLibraryLogLevel = LogLevel.Warning)
    .AddSingleton<DeviceInteraction>();

using var host = builder.Build();
host.Services.GetRequiredService<DeviceInteraction>().Run();

static string? GetArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

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
