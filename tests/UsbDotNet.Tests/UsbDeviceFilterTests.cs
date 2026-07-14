using UsbDotNet.Descriptor;

namespace UsbDotNet.Tests;

public sealed class UsbDeviceFilterTests
{
    private static UsbDeviceDescriptor Descriptor(
        ushort vendorId = 0x2BD9,
        ushort productId = 0x0021,
        ushort bcdUsb = 0x0200
    ) =>
        new()
        {
            VendorId = vendorId,
            ProductId = productId,
            BcdUsb = bcdUsb,
            DeviceKey = UsbDeviceDescriptor.GetKey(vendorId, productId, 1, 1),
        };

    [Fact]
    public void Any_matches_every_device()
    {
        UsbDeviceFilter.Any.Matches(Descriptor()).Should().BeTrue();
        UsbDeviceFilter
            .Any.Matches(Descriptor(vendorId: 0x1234, productId: 0x5678))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void VendorIds_filter_matches_only_the_given_vendors()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x2BD9, 0x1234]);
        filter.Matches(Descriptor(vendorId: 0x2BD9)).Should().BeTrue();
        filter.Matches(Descriptor(vendorId: 0x1234)).Should().BeTrue();
        filter.Matches(Descriptor(vendorId: 0x5678)).Should().BeFalse();
    }

    [Fact]
    public void A_null_VendorIds_filter_matches_every_vendor()
    {
        var filter = new UsbDeviceFilter(VendorIds: null);
        filter.Matches(Descriptor(vendorId: 0x2BD9)).Should().BeTrue();
        filter.Matches(Descriptor(vendorId: 0x1234)).Should().BeTrue();
    }

    [Fact]
    public void An_empty_VendorIds_filter_matches_no_vendor()
    {
        // An empty (non-null) collection means "vendor ID must be in the collection" and no
        // vendor ID is; mirrors the ProductIds behavior below.
        var filter = new UsbDeviceFilter(VendorIds: []);
        filter.Matches(Descriptor(vendorId: 0x2BD9)).Should().BeFalse();
        filter.Matches(Descriptor(vendorId: 0x1234)).Should().BeFalse();
    }

    [Fact]
    public void ProductIds_filter_matches_only_the_given_products()
    {
        var filter = new UsbDeviceFilter(ProductIds: [0x0021, 0x0031]);
        filter.Matches(Descriptor(productId: 0x0021)).Should().BeTrue();
        filter.Matches(Descriptor(productId: 0x0031)).Should().BeTrue();
        filter.Matches(Descriptor(productId: 0x0099)).Should().BeFalse();
    }

    [Fact]
    public void A_null_ProductIds_filter_matches_every_product()
    {
        var filter = new UsbDeviceFilter(ProductIds: null);
        filter.Matches(Descriptor(productId: 0x0021)).Should().BeTrue();
        filter.Matches(Descriptor(productId: 0x0099)).Should().BeTrue();
    }

    [Fact]
    public void An_empty_ProductIds_filter_matches_no_product()
    {
        // An empty (non-null) collection means "product ID must be in the collection" and no
        // product ID is; this mirrors the former GetDeviceList(vendorId, productIds) behavior
        // where an empty HashSet returned an empty device list.
        var filter = new UsbDeviceFilter(ProductIds: []);
        filter.Matches(Descriptor(productId: 0x0021)).Should().BeFalse();
        filter.Matches(Descriptor(productId: 0x0099)).Should().BeFalse();
    }

    [Fact]
    public void Combined_filter_requires_all_fields_to_match()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x2BD9], ProductIds: [0x0021]);
        filter.Matches(Descriptor(vendorId: 0x2BD9, productId: 0x0021)).Should().BeTrue();
        filter.Matches(Descriptor(vendorId: 0x2BD9, productId: 0x0099)).Should().BeFalse();
        filter.Matches(Descriptor(vendorId: 0x1234, productId: 0x0021)).Should().BeFalse();
    }

    [Fact]
    public void A_zeroed_descriptor_never_matches_any_filter()
    {
        // The Windows backend synthesizes descriptors with BcdUsb == 0 for root hubs and for
        // devices whose real descriptor could not be read; their device keys cannot be resolved
        // by the rest of the API, so no filter may ever match them.
        UsbDeviceFilter.Any.Matches(Descriptor(bcdUsb: 0)).Should().BeFalse();
        new UsbDeviceFilter(VendorIds: [0x2BD9])
            .Matches(Descriptor(vendorId: 0x2BD9, bcdUsb: 0))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ToString_formats_VendorIds_and_ProductIds_as_hex()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x2BD9, 0x1234], ProductIds: [0x0021, 0x0031]);
        filter
            .ToString()
            .Should()
            .Be("UsbDeviceFilter(VendorId='0x2BD9,0x1234', ProductIds='0x0021,0x0031')");
    }

    [Fact]
    public void ToString_renders_a_null_VendorIds_as_a_wildcard()
    {
        var filter = new UsbDeviceFilter(VendorIds: null, ProductIds: [0x0021]);
        filter.ToString().Should().Be("UsbDeviceFilter(VendorId='*', ProductIds='0x0021')");
    }

    [Fact]
    public void ToString_renders_an_empty_VendorIds_collection_as_empty_not_a_wildcard()
    {
        // Unlike null (which matches every vendor and renders as '*'), an empty collection
        // matches no vendor; ToString distinguishes the two rather than rendering both as '*'.
        var filter = new UsbDeviceFilter(VendorIds: [], ProductIds: [0x0021]);
        filter.ToString().Should().Be("UsbDeviceFilter(VendorId='', ProductIds='0x0021')");
    }

    [Fact]
    public void ToString_renders_null_ProductIds_as_a_wildcard()
    {
        var filter = new UsbDeviceFilter(VendorIds: [0x2BD9], ProductIds: null);
        filter.ToString().Should().Be("UsbDeviceFilter(VendorId='0x2BD9', ProductIds='*')");
    }

    [Fact]
    public void ToString_renders_an_empty_ProductIds_collection_as_empty_not_a_wildcard()
    {
        // Unlike null (which matches every product and renders as '*'), an empty collection
        // matches no product; ToString distinguishes the two rather than rendering both as '*'.
        var filter = new UsbDeviceFilter(VendorIds: [0x2BD9], ProductIds: []);
        filter.ToString().Should().Be("UsbDeviceFilter(VendorId='0x2BD9', ProductIds='')");
    }

    [Fact]
    public void Any_ToString_renders_both_fields_as_a_wildcard()
    {
        UsbDeviceFilter.Any.ToString().Should().Be("UsbDeviceFilter(VendorId='*', ProductIds='*')");
    }
}
