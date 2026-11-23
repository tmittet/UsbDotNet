using UsbDotNet.Descriptor;

namespace UsbDotNet.Tests.Descriptor;

public sealed class UsbEndpointDescriptorTest
{
    [Theory]
    // Full-speed and high-speed bulk, and a SuperSpeed bulk endpoint
    [InlineData(0x0040, 64, 0)]
    [InlineData(0x0200, 512, 0)]
    [InlineData(0x0400, 1024, 0)]
    // High-speed, high-bandwidth endpoints with 1 and 2 additional transactions
    [InlineData(0x0C00, 1024, 1)]
    [InlineData(0x1400, 1024, 2)]
    [InlineData(0x1200, 512, 2)]
    // Unknown packet size
    [InlineData(0x0000, 0, 0)]
    // The largest packet size value, and the reserved bits 13-15 are ignored
    [InlineData(0x07FF, 2047, 0)]
    [InlineData(0xE400, 1024, 0)]
    public void MaxPacketSize_and_AdditionalTransactions_are_split_from_the_raw_value(
        ushort rawValue,
        ushort expectedMaxPacketSize,
        byte expectedAdditionalTransactions
    )
    {
        IUsbEndpointDescriptor descriptor = new UsbEndpointDescriptor(
            new UsbEndpointAddress(0x81),
            new UsbEndpointAttributes((byte)UsbEndpointTransferType.Interrupt),
            rawValue,
            Interval: 1,
            Refresh: 0,
            SynchAddress: 0,
            ExtraBytes: ReadOnlyMemory<byte>.Empty
        );

        descriptor.MaxPacketSize.Should().Be(expectedMaxPacketSize);
        descriptor.AdditionalTransactions.Should().Be(expectedAdditionalTransactions);
    }

    [Fact]
    public void Endpoint_addresses_with_the_same_raw_value_are_equal()
    {
        var address = new UsbEndpointAddress(0x81);

        address.Should().Be(new UsbEndpointAddress(0x81));
        address.Should().NotBe(new UsbEndpointAddress(0x01));
        address.Direction.Should().Be(UsbEndpointDirection.Input);
        address.Number.Should().Be(UsbEndpointNumber.Ep01);
    }

    [Fact]
    public void Endpoint_attributes_with_the_same_raw_value_are_equal()
    {
        var attributes = new UsbEndpointAttributes((byte)UsbEndpointTransferType.Bulk);

        attributes.Should().Be(new UsbEndpointAttributes((byte)UsbEndpointTransferType.Bulk));
        attributes
            .Should()
            .NotBe(new UsbEndpointAttributes((byte)UsbEndpointTransferType.Interrupt));
        attributes.TransferType.Should().Be(UsbEndpointTransferType.Bulk);
    }
}
