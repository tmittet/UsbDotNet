using UsbDotNet.Transfer;

namespace UsbDotNet.Internal.Transfer;

internal static class ControlRequestPacket
{
    internal const int SetupSize = 8;

    /// <summary>
    /// Create a read control request packet from given parameters.
    /// </summary>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="type">The control request type; standard, class or vendor</param>
    /// <param name="request">The USB standard spec, class spec or vendor defined request</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="length">Read buffer length or 0 when request has no payload.</param>
    internal static byte[] CreateRead(
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte request,
        ushort value,
        ushort index,
        ushort length
    ) => Create(ControlRequestDirection.In, recipient, type, request, value, index, length);

    /// <summary>
    /// Create a write control request packet from given parameters.
    /// </summary>
    /// <param name="recipient">The recipient of the control request</param>
    /// <param name="type">The control request type; standard, class or vendor</param>
    /// <param name="request">The USB standard spec, class spec or vendor defined request</param>
    /// <param name="value">The value field for the setup packet</param>
    /// <param name="index">The index field for the setup packet</param>
    /// <param name="length">Write buffer length or 0 when request has no payload.</param>
    internal static byte[] CreateWrite(
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte request,
        ushort value,
        ushort index,
        ushort length
    ) => Create(ControlRequestDirection.Out, recipient, type, request, value, index, length);

    private static byte[] Create(
        ControlRequestDirection direction,
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte request,
        ushort value,
        ushort index,
        ushort length
    )
    {
        var buffer = new byte[SetupSize + length];
        buffer[0] = (byte)(((byte)direction << 7) | ((byte)type << 5) | (byte)recipient);
        buffer[1] = request;
        buffer[2] = (byte)(value & 0xFF);
        buffer[3] = (byte)(value >> 8);
        buffer[4] = (byte)(index & 0xFF);
        buffer[5] = (byte)(index >> 8);
        buffer[6] = (byte)(length & 0xFF);
        buffer[7] = (byte)(length >> 8);
        return buffer;
    }
}
