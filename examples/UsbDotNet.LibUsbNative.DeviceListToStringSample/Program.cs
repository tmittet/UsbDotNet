using System.Text;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

// ------------------------------------------------------------
// Program
// ------------------------------------------------------------
var libusb = new UsbDotNet.LibUsbNative.LibUsb();
var context = libusb.CreateContext();

Console.WriteLine($"LibUsb version: {libusb.GetVersion()}");

context.RegisterLogCallback(
    (level, message) =>
    {
        Console.WriteLine($"[LibUsb][{level}] {message}");
    }
);

using var deviceList = context.GetDeviceList();
Console.WriteLine($"Found {deviceList.Count} USB devices.");

var idx = 0;
foreach (var device in deviceList)
{
    Console.WriteLine($"=== Device #{idx} ===");
    SamplePrinter.PrintDevice(device);
    Console.WriteLine();
    idx++;
}

static class SamplePrinter
{
    public static void PrintDevice(ISafeDevice device)
    {
        var devDesc = device.GetDeviceDescriptor();

        // Collect all configuration descriptors now that GetConfigDescriptor is available.
        var configs = new List<libusb_config_descriptor>();
        for (byte i = 0; i < devDesc.bNumConfigurations; i++)
        {
            try
            {
                var cfg = device.GetConfigDescriptor(i);
                configs.Add(cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [WARN] Failed to get configuration {i}: {ex.Message}");
            }
        }

        // Use existing descriptor helper
        string tree = configs.Count == 0 ? devDesc.ToTreeString() : devDesc.ToTreeString(configs);

        // Remap indentation from internal 2-space steps to requested 4-space steps
        string remapped = RemapIndent(tree);
        Console.WriteLine(remapped);

        // Read all referenced string descriptors
        try
        {
            using var handle = device.Open();
            var sb = new StringBuilder();
            bool any = false;

            void AddStringLine(string label, byte index, string extraContext = "")
            {
                if (index == 0)
                    return;
                var s = SafeGetString(handle, index);
                if (string.IsNullOrEmpty(s))
                    return;
                if (!any)
                {
                    sb.AppendLine(Indent(1) + "Strings:");
                    any = true;
                }
                var ctx = string.IsNullOrEmpty(extraContext) ? "" : $" {extraContext}";
                sb.AppendLine(Indent(2) + $"{label}{ctx} (index {index}): \"{s}\"");
            }

            AddStringLine("Manufacturer", devDesc.iManufacturer);
            AddStringLine("Product", devDesc.iProduct);
            AddStringLine("SerialNumber", devDesc.iSerialNumber);

            foreach (var cfg in configs)
            {
                AddStringLine(
                    "Configuration",
                    cfg.iConfiguration,
                    $"(bWOOPConfigurationValue={cfg.bConfigurationValue})"
                );
                // Interface + alt setting strings
                for (int i = 0; i < cfg.interfaces.Count; i++)
                {
                    var iface = cfg.interfaces[i];
                    foreach (var alt in iface.altsetting)
                    {
                        if (alt.iInterface == 0)
                            continue;
                        AddStringLine(
                            "Interface",
                            alt.iInterface,
                            $"(cfg={cfg.bConfigurationValue}, if={alt.bInterfaceNumber}, alt={alt.bAlternateSetting})"
                        );
                    }
                }
            }

            if (any)
                Console.WriteLine(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                Indent(1) + $"[WARN] Failed to read string descriptors: {ex.Message}"
            );
        }
    }

    private static string SafeGetString(ISafeDeviceHandle handle, byte index)
    {
        try
        {
            return handle.GetStringDescriptorAscii(index);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string RemapIndent(string input)
    {
        var lines = input.Split(["\r\n", "\n"], StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            int count = 0;
            while (count < lines[i].Length && lines[i][count] == ' ')
                count++;
            if (count == 0)
                continue;
            int logicalLevels = count / 2; // original uses 2 spaces per logical level
            lines[i] = new string(' ', logicalLevels * 4) + lines[i][count..];
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string Indent(int level) => new string(' ', level * 4);
}

/*
Changes:
- Enumerates all configurations using GetConfigDescriptor(byte).
- Reuses DescriptorPrintExtensions.ToTreeString for device + all configs.
- Maintains requested 4-space hierarchy via remapping.
- Collects and prints all relevant string descriptors (device, each configuration, each interface alt).
*/
