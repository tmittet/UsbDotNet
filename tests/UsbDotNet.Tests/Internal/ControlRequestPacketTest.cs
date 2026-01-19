using UsbDotNet.Internal.Transfer;
using UsbDotNet.Transfer;

namespace UsbDotNet.Tests.Internal;

public class ControlRequestPacketTest
{
    [Theory]
    [InlineData(ControlRequestRecipient.Device, ControlRequestType.Standard, 0b10000000)]
    [InlineData(ControlRequestRecipient.Interface, ControlRequestType.Standard, 0b10000001)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Standard, 0b10000010)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Class, 0b10100010)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Vendor, 0b11000010)]
    public void CreateRead_returns_expected_setup_packet_request_byte(
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte expectedValue
    )
    {
        var buffer = ControlRequestPacket.CreateRead(recipient, type, 0, 0, 0, 0);
        buffer[0].Should().Be(expectedValue, Convert.ToString(buffer[0], 2).PadLeft(8, '0'));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(3, 11)]
    [InlineData(7, 15)]
    public void CreateRead_returns_expected_setup_packet_length(
        ushort payloadLength,
        int expectedLength
    )
    {
        var buffer = ControlRequestPacket.CreateRead(
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            0,
            0,
            0,
            payloadLength
        );
        buffer.Should().HaveCount(expectedLength);
    }

    [Theory]
    [InlineData(ControlRequestRecipient.Device, ControlRequestType.Standard, 0b00000000)]
    [InlineData(ControlRequestRecipient.Interface, ControlRequestType.Standard, 0b00000001)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Standard, 0b00000010)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Class, 0b00100010)]
    [InlineData(ControlRequestRecipient.Endpoint, ControlRequestType.Vendor, 0b01000010)]
    public void CreateWrite_returns_expected_setup_packet_request_byte(
        ControlRequestRecipient recipient,
        ControlRequestType type,
        byte expectedValue
    )
    {
        var buffer = ControlRequestPacket.CreateWrite(recipient, type, 0, 0, 0, 0);
        buffer[0].Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(4, 12)]
    [InlineData(9, 17)]
    public void CreateWrite_returns_expected_setup_packet_length(
        ushort payloadLength,
        int expectedLength
    )
    {
        var buffer = ControlRequestPacket.CreateWrite(
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            0,
            0,
            0,
            payloadLength
        );
        buffer.Should().HaveCount(expectedLength);
    }
}
