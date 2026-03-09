using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// P/Invoke declarations for CfgMgr32.dll (Configuration Manager) and a helper
/// for resolving the USB serial number of a parent composite device node.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class CfgMgrInterop
{
    /// <summary>
    /// Resolves the USB serial number for the parent composite device of the given DirectShow
    /// device path by walking up the device tree via <c>CM_Get_Parent</c>.
    /// </summary>
    /// <param name="devicePath">
    /// The DirectShow device path, e.g.
    /// <c>\\?\USB#VID_2BD9&amp;PID_A032&amp;MI_00#D&amp;54E8F4A&amp;3&amp;0000#{...}\GLOBAL</c>.
    /// </param>
    /// <returns>
    /// The serial number component of the parent device instance ID
    /// (e.g. <c>52322F0017-12101A0022</c> from <c>USB\VID_2BD9&amp;PID_A032\52322F0017-12101A0022</c>),
    /// or <see langword="null"/> if the parent node could not be resolved.
    /// </returns>
    internal static string? GetParentSerialNumber(string devicePath)
    {
        // Convert "\\?\USB#VID_XXXX&PID_YYYY&MI_ZZ#D&...#{guid}\GLOBAL"
        // to the device instance ID "USB\VID_XXXX&PID_YYYY&MI_ZZ\D&..."
        var p = devicePath;
        if (p.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            p = p.Substring(4);
        var guidHash = p.IndexOf("#{", StringComparison.Ordinal);
        if (guidHash >= 0)
            p = p.Substring(0, guidHash);
        var instanceId = p.Replace('#', '\\');

        if (CM_Locate_DevNode(out var devInst, instanceId, 0) != 0)
            return null;
        if (CM_Get_Parent(out var parentInst, devInst, 0) != 0)
            return null;

        var buffer = new char[256];
        if (CM_Get_Device_ID(parentInst, buffer, (uint)buffer.Length, 0) != 0)
            return null;

        // Parent instance ID: "USB\VID_XXXX&PID_YYYY\<serial>"
        var parts = new string(buffer).TrimEnd('\0').Split('\\');
        return parts.Length >= 3 ? parts[2] : null;
    }

    // TODO: Fix CA5392 warnings (not possible with .NET6)
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNode(
        out uint pdnDevInst,
        string pDeviceID,
        uint ulFlags
    );

    [DllImport("CfgMgr32.dll")]
    private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID(
        uint dnDevInst,
        char[] Buffer,
        uint BufferLen,
        uint ulFlags
    );
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
}
