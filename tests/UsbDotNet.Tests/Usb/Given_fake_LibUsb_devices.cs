using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.Tests.Fakes;

namespace UsbDotNet.Tests.Usb;

[Trait("Category", "Usb")]
public sealed class Given_fake_LibUsb_devices
{
    private const ushort VendorA = 0x1111;
    private const ushort VendorB = 0x2222;
    private const ushort ProductA1 = 0xAA01;
    private const ushort ProductA2 = 0xAA02;
    private const ushort ProductB1 = 0xBB01;
    private const byte BusNumber = 1;

    [Fact]
    public void GetDeviceList_returns_an_empty_collection_when_there_are_no_devices()
    {
        using var usb = CreateInitializedUsb();

        var devices = usb.GetDeviceList();

        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetDeviceList_returns_all_devices_when_no_filter_is_given()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 3)
        );

        var devices = usb.GetDeviceList();

        devices.Should().HaveCount(3);
    }

    [Fact]
    public void GetDeviceList_returns_all_devices_when_filters_are_null()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 2)
        );

        var devices = usb.GetDeviceList(null, productIds: null);

        devices.Should().HaveCount(2);
    }

    [Fact]
    public void GetDeviceList_returns_only_devices_matching_the_vendorId()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 3)
        );

        var devices = usb.GetDeviceList(VendorA);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(2);
        devices.Should().OnlyContain(d => d.VendorId == VendorA);
    }

    [Fact]
    public void GetDeviceList_returns_empty_when_no_device_matches_the_vendorId()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2)
        );

        var devices = usb.GetDeviceList(VendorB);

        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetDeviceList_returns_only_devices_with_a_productId_in_the_given_set()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 3)
        );

        var devices = usb.GetDeviceList(null, [ProductA1, ProductB1]);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(2);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1 || d.ProductId == ProductB1);
    }

    [Fact]
    public void GetDeviceList_returns_empty_when_an_empty_productId_set_is_given()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 2)
        );

        // Unlike a null set or an empty productId array, an empty set matches no product IDs
        HashSet<ushort> productIds = [];
        var devices = usb.GetDeviceList(null, productIds);

        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetDeviceList_returns_all_devices_when_an_empty_productId_array_is_given()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2)
        );

        // Unlike an empty productId set, an empty productId array means no filter
        ushort[] productIds = [];
        var devices = usb.GetDeviceList(VendorA, productIds);

        devices.Should().HaveCount(2);
    }

    [Fact]
    public void GetDeviceList_filters_on_both_vendorId_and_productIds()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2),
            // Same product ID as an existing VendorA device, but a different vendor
            FakeHelper.CreateFakeDevice(VendorB, ProductA1, BusNumber, busAddress: 3)
        );

        var devices = usb.GetDeviceList(VendorA, [ProductA1]);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.VendorId == VendorA && d.ProductId == ProductA1);
    }

    [Fact]
    public void GetDeviceList_excludes_devices_with_BcdUsb_zero()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2, bcdUsb: 0)
        );

        var devices = usb.GetDeviceList();

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1);
    }

    [Fact]
    public void GetDeviceList_returns_a_descriptor_with_expected_values()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 7)
        );

        var device = usb.GetDeviceList().Single();

        using var scope = new AssertionScope();
        device.VendorId.Should().Be(VendorA);
        device.ProductId.Should().Be(ProductA1);
        device.BusNumber.Should().Be(BusNumber);
        device.BusAddress.Should().Be(7);
        device.DeviceKey.Should().Be(UsbDeviceDescriptor.GetKey(VendorA, ProductA1, BusNumber, 7));
    }

    [Fact]
    public void GetDeviceList_filters_on_a_single_productId()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2)
        );

        var devices = usb.GetDeviceList(VendorA, ProductA2);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.ProductId == ProductA2);
    }

    [Fact]
    public void GetDeviceList_filters_on_multiple_productIds()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2),
            FakeHelper.CreateFakeDevice(VendorB, ProductB1, BusNumber, busAddress: 3)
        );

        var devices = usb.GetDeviceList(null, ProductA1, ProductB1);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(2);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1 || d.ProductId == ProductB1);
    }

    [Fact]
    public void GetDeviceList_ignores_productIds_that_match_no_device()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1),
            FakeHelper.CreateFakeDevice(VendorA, ProductA2, BusNumber, busAddress: 2)
        );

        var devices = usb.GetDeviceList(VendorA, [ProductA1, 0x0099]);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1);
    }

    [Fact]
    public void GetDeviceList_ignores_duplicate_productIds()
    {
        using var usb = CreateInitializedUsb(
            FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1)
        );

        var devices = usb.GetDeviceList(VendorA, ProductA1, ProductA1);

        devices.Should().HaveCount(1);
    }

    [Fact]
    public void GetDeviceList_throws_InvalidOperationException_when_Usb_is_not_initialized()
    {
        using var usb = new UsbDotNet.Usb(FakeHelper.CreateFakeLibUsb());

        var act = () => usb.GetDeviceList();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDeviceList_throws_ObjectDisposedException_when_Usb_is_disposed()
    {
        var usb = CreateInitializedUsb();
        usb.Dispose();

        var act = () => usb.GetDeviceList();

        act.Should().Throw<ObjectDisposedException>();
    }

    private static UsbDotNet.Usb CreateInitializedUsb(params ISafeDevice[] devices)
    {
        var usb = new UsbDotNet.Usb(FakeHelper.CreateFakeLibUsb(devices));
        try
        {
            usb.Initialize();
        }
        catch
        {
            usb.Dispose();
            throw;
        }
        return usb;
    }
}
