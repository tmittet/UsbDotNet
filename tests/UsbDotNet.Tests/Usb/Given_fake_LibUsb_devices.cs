using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 3)
        );

        var devices = usb.GetDeviceList();

        devices.Should().HaveCount(3);
    }

    [Fact]
    public void GetDeviceList_returns_all_devices_when_filters_are_null()
    {
        using var usb = CreateInitializedUsb(
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 2)
        );

        var devices = usb.GetDeviceList(null, productIds: null);

        devices.Should().HaveCount(2);
    }

    [Fact]
    public void GetDeviceList_returns_only_devices_matching_the_vendorId()
    {
        using var usb = CreateInitializedUsb(
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 3)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2)
        );

        var devices = usb.GetDeviceList(VendorB);

        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetDeviceList_returns_only_devices_with_a_productId_in_the_given_set()
    {
        using var usb = CreateInitializedUsb(
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 3)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 2)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2),
            // Same product ID as an existing VendorA device, but a different vendor
            CreateFakeDevice(VendorB, ProductA1, busAddress: 3)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2, bcdUsb: 0)
        );

        var devices = usb.GetDeviceList();

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1);
    }

    [Fact]
    public void GetDeviceList_returns_a_descriptor_with_expected_values()
    {
        using var usb = CreateInitializedUsb(CreateFakeDevice(VendorA, ProductA1, busAddress: 7));

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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2),
            CreateFakeDevice(VendorB, ProductB1, busAddress: 3)
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
            CreateFakeDevice(VendorA, ProductA1, busAddress: 1),
            CreateFakeDevice(VendorA, ProductA2, busAddress: 2)
        );

        var devices = usb.GetDeviceList(VendorA, [ProductA1, 0x0099]);

        using var scope = new AssertionScope();
        devices.Should().HaveCount(1);
        devices.Should().OnlyContain(d => d.ProductId == ProductA1);
    }

    [Fact]
    public void GetDeviceList_ignores_duplicate_productIds()
    {
        using var usb = CreateInitializedUsb(CreateFakeDevice(VendorA, ProductA1, busAddress: 1));

        var devices = usb.GetDeviceList(VendorA, ProductA1, ProductA1);

        devices.Should().HaveCount(1);
    }

    [Fact]
    public void GetDeviceList_throws_InvalidOperationException_when_Usb_is_not_initialized()
    {
        using var usb = new UsbDotNet.Usb(CreateFakeLibUsb());

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
        var usb = new UsbDotNet.Usb(CreateFakeLibUsb(devices));
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

    private static ILibUsb CreateFakeLibUsb(params ISafeDevice[] devices)
    {
        var libUsb = A.Fake<ILibUsb>();
        A.CallTo(() => libUsb.CreateContext()).ReturnsLazily(() => CreateFakeContext(devices));
        return libUsb;
    }

    private static ISafeContext CreateFakeContext(IReadOnlyList<ISafeDevice> devices)
    {
        var context = A.Fake<ISafeContext>();
#pragma warning disable CA2000 // Disposed by the context Dispose callback configured below
        var interrupted = new SemaphoreSlim(0);
#pragma warning restore CA2000
        var closed = false;
        A.CallTo(() => context.GetDeviceList()).ReturnsLazily(() => CreateFakeDeviceList(devices));
        // Block the event loop thread until interrupted, like the real blocking call
        A.CallTo(() => context.HandleEventsCompleted(A<nint>._))
            .ReturnsLazily(() =>
            {
                interrupted.Wait();
                return libusb_error.LIBUSB_SUCCESS;
            });
        A.CallTo(() => context.InterruptEventHandler()).Invokes(() => interrupted.Release());
        // The event loop thread is joined before the context is disposed,
        // making it safe to dispose the semaphore here
        A.CallTo(() => context.Dispose())
            .Invokes(() =>
            {
                closed = true;
                interrupted.Dispose();
            });
        A.CallTo(() => context.IsClosed).ReturnsLazily(() => closed);
        return context;
    }

    private static ISafeDeviceList CreateFakeDeviceList(IReadOnlyList<ISafeDevice> devices)
    {
        var deviceList = A.Fake<ISafeDeviceList>();
        A.CallTo(() => deviceList.Count).Returns(devices.Count);
        A.CallTo(() => deviceList[A<int>._]).ReturnsLazily((int index) => devices[index]);
        A.CallTo(() => deviceList.GetEnumerator()).ReturnsLazily(() => devices.GetEnumerator());
        return deviceList;
    }

    private static ISafeDevice CreateFakeDevice(
        ushort vendorId,
        ushort productId,
        byte busAddress,
        ushort bcdUsb = 0x0200
    )
    {
        var device = A.Fake<ISafeDevice>();
        A.CallTo(() => device.GetDeviceDescriptor())
            .Returns(
                new libusb_device_descriptor(
                    bLength: 18,
                    libusb_descriptor_type.LIBUSB_DT_DEVICE,
                    bcdUsb,
                    libusb_class_code.LIBUSB_CLASS_PER_INTERFACE,
                    bDeviceSubClass: 0,
                    bDeviceProtocol: 0,
                    bMaxPacketSize0: 64,
                    vendorId,
                    productId,
                    bcdDevice: 0x0100,
                    iManufacturer: 1,
                    iProduct: 2,
                    iSerialNumber: 3,
                    bNumConfigurations: 1
                )
            );
        A.CallTo(() => device.GetBusNumber()).Returns(BusNumber);
        A.CallTo(() => device.GetDeviceAddress()).Returns(busAddress);
        A.CallTo(() => device.GetPortNumber()).Returns((byte)1);
        return device;
    }
}
