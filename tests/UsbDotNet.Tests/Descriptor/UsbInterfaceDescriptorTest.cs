using UsbDotNet.Descriptor;

namespace UsbDotNet.Tests.Descriptor;

public sealed class UsbInterfaceDescriptorTest
{
    [Fact]
    public void GetEndpoints_returns_the_endpoints_with_the_given_direction_in_descriptor_order()
    {
        var descriptor = InterfaceWithEndpoints(0x82, 0x01, 0x81);

        var inputs = descriptor.GetEndpoints(UsbEndpointDirection.Input);
        var outputs = descriptor.GetEndpoints(UsbEndpointDirection.Output);

        inputs.Select(e => e.EndpointAddress.RawValue).Should().Equal(0x82, 0x81);
        outputs.Select(e => e.EndpointAddress.RawValue).Should().Equal(0x01);
    }

    [Fact]
    public void GetEndpoints_returns_an_empty_list_when_no_endpoint_has_the_given_direction()
    {
        var descriptor = InterfaceWithEndpoints(0x81);

        descriptor.GetEndpoints(UsbEndpointDirection.Output).Should().BeEmpty();
    }

    private static IUsbInterfaceDescriptor InterfaceWithEndpoints(params byte[] addresses) =>
        new UsbInterfaceDescriptor(
            InterfaceNumber: 0,
            AlternateSetting: 0,
            InterfaceClass: UsbClass.VendorSpecific,
            InterfaceSubClass: 0,
            InterfaceProtocol: 0,
            StringDescriptorIndex: 0,
            ExtraBytes: ReadOnlyMemory<byte>.Empty,
            Endpoints: addresses
                .Select(address =>
                    (IUsbEndpointDescriptor)
                        new UsbEndpointDescriptor(
                            new UsbEndpointAddress(address),
                            new UsbEndpointAttributes((byte)UsbEndpointTransferType.Bulk),
                            MaxPacketSizeRawValue: 512,
                            Interval: 0,
                            Refresh: 0,
                            SyncAddress: 0,
                            ExtraBytes: ReadOnlyMemory<byte>.Empty
                        )
                )
                .ToList()
        );
}
