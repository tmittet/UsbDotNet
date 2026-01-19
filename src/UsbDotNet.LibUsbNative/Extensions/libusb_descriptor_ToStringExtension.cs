using System.Globalization;
using System.Text;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.Extensions;

// -----------------------
// Tree/structured printing (unchanged – still rich / human readable)
// -----------------------
public static class libusb_descriptor_ToStringExtension
{
    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private static readonly string[] _newLine = new[] { "\r\n", "\n" };

    public static string ToTreeString(this libusb_device_descriptor d)
    {
        var sb = new StringBuilder()
            .AppendLine("Device Descriptor:")
            .AppendLine(_culture, $"  bLength              : {d.bLength}")
            .AppendLine(_culture, $"  bDescriptorType      : {Fmt(d.bDescriptorType)}")
            .AppendLine(_culture, $"  bcdUSB               : 0x{d.bcdUSB:X4}")
            .AppendLine(_culture, $"  bDeviceClass         : {Fmt(d.bDeviceClass)}")
            .AppendLine(_culture, $"  bDeviceSubClass      : 0x{d.bDeviceSubClass:X2}")
            .AppendLine(_culture, $"  bDeviceProtocol      : 0x{d.bDeviceProtocol:X2}")
            .AppendLine(_culture, $"  bMaxPacketSize0      : {d.bMaxPacketSize0}")
            .AppendLine(_culture, $"  idVendor             : 0x{d.idVendor:X4}")
            .AppendLine(_culture, $"  idProduct            : 0x{d.idProduct:X4}")
            .AppendLine(_culture, $"  bcdDevice            : 0x{d.bcdDevice:X4}")
            .AppendLine(_culture, $"  iManufacturer        : {d.iManufacturer}")
            .AppendLine(_culture, $"  iProduct             : {d.iProduct}")
            .AppendLine(_culture, $"  iSerialNumber        : {d.iSerialNumber}")
            .AppendLine(_culture, $"  bNumConfigurations   : {d.bNumConfigurations}");

        return sb.ToString().TrimEnd();
    }

    public static string ToTreeString(
        this libusb_device_descriptor d,
        IReadOnlyList<libusb_config_descriptor> configs
    )
    {
        var sb = new StringBuilder().AppendLine(d.ToTreeString());
        for (var i = 0; i < configs.Count; i++)
        {
            _ = sb.AppendLine().Append(configs[i].ToTreeString().Indent(2));
        }
        return sb.ToString().TrimEnd();
    }

    public static string ToTreeString(this libusb_config_descriptor cfg)
    {
        var sb = new StringBuilder()
            .AppendLine("Configuration Descriptor:")
            .AppendLine(_culture, $"  bLength             : {cfg.bLength}")
            .AppendLine(_culture, $"  bDescriptorType     : {Fmt(cfg.bDescriptorType)}")
            .AppendLine(_culture, $"  wTotalLength        : {cfg.wTotalLength}")
            .AppendLine(_culture, $"  bNumInterfaces      : {cfg.bNumInterfaces}")
            .AppendLine(_culture, $"  bConfigurationValue : {cfg.bConfigurationValue}")
            .AppendLine(_culture, $"  iConfiguration      : {cfg.iConfiguration}")
            .AppendLine(_culture, $"  bmAttributes        : {Fmt(cfg.bmAttributes)}")
            .AppendLine(_culture, $"  MaxPower            : {cfg.bMaxPower} (units of 2mA)");
        if (cfg.extra is { Length: > 0 })
        {
            _ = sb.AppendLine(_culture, $"  Extra               : {cfg.extra.Length} bytes");
        }
        for (var i = 0; i < cfg.interfaces.Count; i++)
        {
            var iface = cfg.interfaces[i];
            _ = sb.AppendLine().AppendLine(_culture, $"  Interface[{i}]:");
            for (var a = 0; a < iface.altsetting.Count; a++)
            {
                var alt = iface.altsetting[a];
                _ = sb.Append(alt.ToTreeString().Indent(4));
            }
        }
        return sb.ToString().TrimEnd();
    }

    public static string ToTreeString(this libusb_interface_descriptor id)
    {
        var sb = new StringBuilder()
            .AppendLine("Interface Descriptor:")
            .AppendLine(_culture, $"  bLength           : {id.bLength}")
            .AppendLine(_culture, $"  bDescriptorType   : {Fmt(id.bDescriptorType)}")
            .AppendLine(_culture, $"  bInterfaceNumber  : {id.bInterfaceNumber}")
            .AppendLine(_culture, $"  bAlternateSetting : {id.bAlternateSetting}")
            .AppendLine(_culture, $"  bNumEndpoints     : {id.bNumEndpoints}")
            .AppendLine(_culture, $"  bInterfaceClass   : {Fmt(id.bInterfaceClass)}")
            .AppendLine(_culture, $"  bInterfaceSubClass: 0x{id.bInterfaceSubClass:X2}")
            .AppendLine(_culture, $"  bInterfaceProtocol: 0x{id.bInterfaceProtocol:X2}")
            .AppendLine(_culture, $"  iInterface        : {id.iInterface}");

        if (id.extra is { Length: > 0 })
        {
            _ = sb.AppendLine(_culture, $"  Extra             : {id.extra.Length} bytes");
        }
        foreach (var ep in id.endpoints)
        {
            _ = sb.AppendLine().Append(ep.ToTreeString().Indent(2));
        }
        return sb.ToString();
    }

    public static string ToTreeString(this libusb_endpoint_descriptor ep)
    {
        var sb = new StringBuilder()
            .AppendLine("Endpoint Descriptor:")
            .AppendLine(_culture, $"  bLength          : {ep.bLength}")
            .AppendLine(_culture, $"  bDescriptorType  : {Fmt(ep.bDescriptorType)}")
            .AppendLine(
                _culture,
                $"  bEndpointAddress : 0x{ep.bEndpointAddress.RawValue:X2} ({ep.bEndpointAddress.Direction}, {ep.bEndpointAddress.Number})"
            )
            .AppendLine(
                _culture,
                $"  bmAttributes     : 0x{ep.bmAttributes.RawValue:X2} ({ep.bmAttributes.TransferType}/{ep.bmAttributes.SyncType}/{ep.bmAttributes.UsageType})"
            )
            .AppendLine(_culture, $"  wMaxPacketSize   : {ep.wMaxPacketSize}")
            .AppendLine(_culture, $"  bInterval        : {ep.bInterval}")
            .AppendLine(_culture, $"  bRefresh         : {ep.bRefresh}")
            .AppendLine(_culture, $"  bSynchAddress    : {ep.bSynchAddress}");

        if (ep.extra is { Length: > 0 })
        {
            _ = sb.AppendLine(_culture, $"  Extra            : {ep.extra.Length} bytes");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Enum formatting helper (for tree output)
    /// </summary>
    private static string Fmt<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var raw = Convert.ToUInt64(value, _culture);
        var rawStr =
            raw <= 0xFF ? $"0x{raw:X2}"
            : raw <= 0xFFFF ? $"0x{raw:X4}"
            : $"0x{raw:X}";
        return $"{value} ({rawStr})";
    }

    /// <summary>
    /// Indent lines helper (for tree output)
    /// </summary>
    private static string Indent(this string s, int spaces)
    {
        var pad = new string(' ', spaces);
        var lines = s.Split(_newLine, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = pad + lines[i];
        }
        return string.Join(Environment.NewLine, lines);
    }
}
