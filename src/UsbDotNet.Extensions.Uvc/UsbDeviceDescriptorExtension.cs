using System.Buffers.Binary;
using UsbDotNet.Descriptor;

namespace UsbDotNet.Extensions.Uvc;

/// <summary>
/// Parses UVC VideoControl class-specific interface descriptors to discover entity IDs.
/// </summary>
/// <remarks>
/// The UVC spec (USB_Video_Class_1.5, section 3.7) defines class-specific descriptors
/// appended to the VideoControl interface via <c>ExtraBytes</c>. Each descriptor begins:
/// <code>
/// Offset 0: bLength
/// Offset 1: bDescriptorType (0x24 = CS_INTERFACE)
/// Offset 2: bDescriptorSubtype
/// </code>
/// Relevant subtypes:
/// <list type="bullet">
/// <item>
/// 0x02 VC_INPUT_TERMINAL — byte 3 = bTerminalID, bytes 4–5 = wTerminalType (0x0201 = ITT_CAMERA)
/// </item>
/// <item>0x05 VC_PROCESSING_UNIT — byte 3 = bUnitID</item>
/// <item>0x06 VC_EXTENSION_UNIT — byte 3 = bUnitID, bytes 4–19 = guidExtensionCode</item>
/// </list>
/// </remarks>
public static class UsbDeviceDescriptorExtension
{
    /// <summary>
    /// SC_VIDEOCONTROL
    /// </summary>
    internal const byte UvcVideoControlSubClass = 0x01;

    private const byte CsInterface = 0x24;
    private const ushort IttCamera = 0x0201;

    private const byte VcInputTerminal = 0x02;
    private const byte VcProcessingUnit = 0x05;
    private const byte VcExtensionUnit = 0x06;

    /// <summary>
    /// Returns the VideoControl interface descriptor for the specified interface number, or null.
    /// </summary>
    public static IUsbInterfaceDescriptor? GetUvcInterfaceDescriptor(
        this IUsbDevice? device,
        byte interfaceNumber
    ) =>
        device is not null
        && device.ConfigDescriptor.Interfaces.TryGetValue(interfaceNumber, out var altSettings)
        // Per the USB spec, alternate setting 0 always exists and is the default alternate setting
        && altSettings.TryGetValue(0, out var usbInterface)
        && usbInterface.InterfaceClass == UsbClass.Video
        && usbInterface.InterfaceSubClass == UvcVideoControlSubClass
            ? usbInterface
            : null;

    /// <summary>
    /// Returns the ID of the Camera Input Terminal on the specified
    /// VideoControl interface, or null if the device has no camera terminal
    /// (e.g. a capture card or a device with no optical controls).
    /// </summary>
    public static byte? GetUvcCameraControlEntityId(this IUsbDevice? device, byte interfaceNumber)
    {
        var usbInterface = GetUvcInterfaceDescriptor(device, interfaceNumber);
        if (usbInterface is null)
            return null;

        var span = usbInterface.ExtraBytes.AsSpan();
        var offset = 0;
        while (TryReadNextCsInterfaceDescriptor(span, ref offset, out var descriptor))
        {
            if (descriptor[2] != VcInputTerminal || descriptor.Length < 6)
                continue;

            var terminalType = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[4..]);
            if (terminalType == IttCamera)
                return descriptor[3]; // bTerminalID
        }
        return null;
    }

    /// <summary>
    /// Returns the ID of the Processing Unit on the specified VideoControl interface,
    /// or null if the device has no processing unit.
    /// </summary>
    public static byte? GetUvcImageSettingEntityId(this IUsbDevice? device, byte interfaceNumber)
    {
        var usbInterface = GetUvcInterfaceDescriptor(device, interfaceNumber);
        if (usbInterface is null)
            return null;

        var span = usbInterface.ExtraBytes.AsSpan();
        var offset = 0;
        while (TryReadNextCsInterfaceDescriptor(span, ref offset, out var descriptor))
        {
            if (descriptor[2] == VcProcessingUnit && descriptor.Length >= 4)
                return descriptor[3]; // bUnitID
        }
        return null;
    }

    /// <summary>
    /// Returns the ID of the Extension Unit on the specified VideoControl interface
    /// whose <c>guidExtensionCode</c> matches <paramref name="extensionGuid"/>;
    /// or null if no matching Extension Unit is found.
    /// </summary>
    public static byte? GetUvcExtensionUnitEntityId(
        this IUsbDevice? device,
        byte interfaceNumber,
        Guid extensionGuid
    )
    {
        var usbInterface = GetUvcInterfaceDescriptor(device, interfaceNumber);
        if (usbInterface is null)
            return null;

        var guidBytes = extensionGuid.ToByteArray();
        var span = usbInterface.ExtraBytes.AsSpan();
        var offset = 0;
        while (TryReadNextCsInterfaceDescriptor(span, ref offset, out var descriptor))
        {
            if (descriptor[2] != VcExtensionUnit || descriptor.Length < 20)
                continue;
            // Guid starts at offset 4 and is 16 bytes. The Guid in the descriptor uses
            // the same mixed-endian layout as Guid.ToByteArray(), so comparison is direct.
            if (descriptor.Slice(4, 16).SequenceEqual(guidBytes))
                return descriptor[3]; // bUnitID
        }
        return null;
    }

    private static bool TryReadNextCsInterfaceDescriptor(
        ReadOnlySpan<byte> span,
        ref int offset,
        out ReadOnlySpan<byte> descriptor
    )
    {
        while (offset < span.Length)
        {
            var length = span[offset];
            if (length < 3 || offset + length > span.Length)
                break;

            descriptor = span.Slice(offset, length);
            offset += length;

            if (descriptor[1] == CsInterface)
                return true;
        }
        descriptor = default;
        return false;
    }
}
