namespace UsbDotNet.Extensions.Uvc.Unix;

/// <summary>
/// Parses UVC VideoControl class-specific interface descriptors to discover entity IDs.
/// </summary>
/// <remarks>
/// The UVC spec (USB_Video_Class_1.5, section 3.7) defines class-specific descriptors
/// appended to the VideoControl interface via <c>ExtraBytes</c>. Each descriptor begins:
/// <code>
/// Offset 0: bLength
/// Offset 1: bDescriptorType  (0x24 = CS_INTERFACE)
/// Offset 2: bDescriptorSubtype
/// </code>
/// Relevant subtypes:
/// <list type="bullet">
/// <item><c>0x02</c> VC_INPUT_TERMINAL  — byte 3 = bTerminalID, bytes 4–5 = wTerminalType (0x0201 = ITT_CAMERA)</item>
/// <item><c>0x05</c> VC_PROCESSING_UNIT — byte 3 = bUnitID</item>
/// <item><c>0x06</c> VC_EXTENSION_UNIT  — byte 3 = bUnitID, bytes 4–19 = guidExtensionCode</item>
/// </list>
/// </remarks>
internal static class UvcDescriptor
{
    public const byte UvcVideoControlSubClass = 0x01; // SC_VIDEOCONTROL
    private const byte CsInterface = 0x24;
    private const ushort IttCamera = 0x0201;
    private const byte VcInputTerminal = 0x02;
    private const byte VcProcessingUnit = 0x05;
    private const byte VcExtensionUnit = 0x06;

    /// <summary>
    /// Returns the <c>bTerminalID</c> of the Camera Input Terminal on the specified VideoControl
    /// interface, or <see langword="null"/> if the device has no camera terminal
    /// (e.g. a capture card or a device with no optical controls).
    /// </summary>
    internal static byte? GetCameraControlEntityId(IUsbDevice device, byte interfaceNumber) =>
        ScanExtraBytes(device, interfaceNumber, subtype: VcInputTerminal);

    /// <summary>
    /// Returns the <c>bUnitID</c> of the Processing Unit on the specified VideoControl interface,
    /// or <see langword="null"/> if the device has no processing unit.
    /// </summary>
    internal static byte? GetImageSettingEntityId(IUsbDevice device, byte interfaceNumber) =>
        ScanExtraBytes(device, interfaceNumber, subtype: VcProcessingUnit);

    /// <summary>
    /// Returns the <c>bUnitID</c> of the Extension Unit on the specified VideoControl interface
    /// whose <c>guidExtensionCode</c> matches <paramref name="extensionGuid"/>,
    /// or <see langword="null"/> if no matching Extension Unit is found.
    /// </summary>
    /// <remarks>
    /// The 16-byte <c>guidExtensionCode</c> in the descriptor uses the same mixed-endian
    /// layout as <see cref="Guid.ToByteArray()"/>, so comparison is direct.
    /// </remarks>
    internal static byte? GetExtensionUnitEntityId(
        IUsbDevice device,
        byte interfaceNumber,
        Guid extensionGuid
    )
    {
        if (
            !device.ConfigDescriptor.Interfaces.TryGetValue(interfaceNumber, out var altSettings)
            || !altSettings.TryGetValue(0, out var iface)
            || iface.InterfaceClass != UsbClass.Video
            || iface.InterfaceSubClass != UvcVideoControlSubClass
        )
        {
            return null;
        }

        // guidExtensionCode starts at offset 4 and is 16 bytes; minimum bLength is 20.
        var guidBytes = extensionGuid.ToByteArray();
        var span = iface.ExtraBytes.AsSpan();
        var offset = 0;

        while (offset < span.Length)
        {
            var bLength = span[offset];

            if (bLength < 3 || offset + bLength > span.Length)
                break;

            if (
                span[offset + 1] == CsInterface
                && span[offset + 2] == VcExtensionUnit
                && bLength >= 20
                && span.Slice(offset + 4, 16).SequenceEqual(guidBytes)
            )
            {
                return span[offset + 3]; // bUnitID
            }

            offset += bLength;
        }

        return null;
    }

    private static byte? ScanExtraBytes(IUsbDevice device, byte interfaceNumber, byte subtype)
    {
        if (
            !device.ConfigDescriptor.Interfaces.TryGetValue(interfaceNumber, out var altSettings)
            || !altSettings.TryGetValue(0, out var iface)
            || iface.InterfaceClass != UsbClass.Video
            || iface.InterfaceSubClass != UvcVideoControlSubClass
        )
        {
            return null;
        }

        var span = iface.ExtraBytes.AsSpan();
        var offset = 0;

        while (offset < span.Length)
        {
            var bLength = span[offset];

            if (bLength < 3 || offset + bLength > span.Length)
                break;

            if (span[offset + 1] == CsInterface && span[offset + 2] == subtype)
            {
                if (subtype == VcInputTerminal)
                {
                    // Only match camera input terminals (wTerminalType = 0x0201).
                    if (bLength >= 6)
                    {
                        var terminalType = (ushort)(span[offset + 4] | (span[offset + 5] << 8));
                        if (terminalType == IttCamera)
                            return span[offset + 3]; // bTerminalID
                    }
                }
                else
                {
                    if (bLength >= 4)
                        return span[offset + 3]; // bUnitID
                }
            }

            offset += bLength;
        }

        return null;
    }
}
