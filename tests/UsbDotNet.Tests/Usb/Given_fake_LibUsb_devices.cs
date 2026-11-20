using FakeItEasy;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;
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

    [Fact]
    public void Device_strings_are_empty_without_asking_the_OS_or_the_device_when_it_has_no_strings()
    {
        var device = FakeHelper.CreateFakeDevice(
            VendorA,
            ProductA1,
            BusNumber,
            busAddress: 1,
            manufacturerIndex: 0,
            productIndex: 0,
            serialNumberIndex: 0
        );
        using var usb = CreateInitializedUsb(device);
        var deviceKey = usb.GetDeviceList().Single().DeviceKey;

        using var scope = new AssertionScope();
        usb.GetDeviceManufacturer(deviceKey).Should().BeEmpty();
        usb.GetDeviceProduct(deviceKey).Should().BeEmpty();
        usb.GetDeviceSerial(deviceKey).Should().BeEmpty();
        string? ignoredValue;
        libusb_error? ignoredError;
        A.CallTo(() =>
                device.TryGetDeviceString(
                    A<libusb_device_string_type>._,
                    out ignoredValue,
                    out ignoredError
                )
            )
            .MustNotHaveHappened();
        A.CallTo(() => device.Open()).MustNotHaveHappened();
    }

    [Fact]
    public void Device_strings_are_read_from_the_device_when_the_operating_system_has_no_value()
    {
        var device = FakeHelper.CreateFakeDevice(VendorA, ProductA1, BusNumber, busAddress: 1);
        FakeOsDeviceString(device, string.Empty);
        // The device is opened to read the string, which requires a config descriptor
        A.CallTo(() => device.GetActiveConfigDescriptor())
            .Returns(
                new libusb_config_descriptor(
                    bLength: 9,
                    libusb_descriptor_type.LIBUSB_DT_CONFIG,
                    wTotalLength: 9,
                    bNumInterfaces: 0,
                    bConfigurationValue: 1,
                    iConfiguration: 0,
                    libusb_config_desc_attributes.NONE,
                    bMaxPower: 50,
                    interfaces: [],
                    extra: []
                )
            );
        var handle = A.Fake<ISafeDeviceHandle>();
        A.CallTo(() => device.Open()).Returns(handle);
        A.CallTo(() => handle.GetStringDescriptorAscii(3)).Returns("SERIAL");
        using var usb = CreateInitializedUsb(device);
        var deviceKey = usb.GetDeviceList().Single().DeviceKey;

        usb.GetDeviceSerial(deviceKey).Should().Be("SERIAL");
    }

    [Theory]
    // The operating speed decides the unit: 2 mA below SuperSpeed, 8 mA from SuperSpeed
    [InlineData(libusb_speed.LIBUSB_SPEED_LOW, 0x0110, 100)]
    [InlineData(libusb_speed.LIBUSB_SPEED_FULL, 0x0200, 100)]
    [InlineData(libusb_speed.LIBUSB_SPEED_HIGH, 0x0210, 100)]
    [InlineData(libusb_speed.LIBUSB_SPEED_SUPER, 0x0300, 400)]
    [InlineData(libusb_speed.LIBUSB_SPEED_SUPER_PLUS, 0x0310, 400)]
    [InlineData(libusb_speed.LIBUSB_SPEED_SUPER_PLUS_X2, 0x0320, 400)]
    // A known speed wins over bcdUSB
    [InlineData(libusb_speed.LIBUSB_SPEED_HIGH, 0x0300, 100)]
    [InlineData(libusb_speed.LIBUSB_SPEED_SUPER, 0x0210, 400)]
    // bcdUSB decides when the operating system does not report the speed
    [InlineData(libusb_speed.LIBUSB_SPEED_UNKNOWN, 0x0210, 100)]
    [InlineData(libusb_speed.LIBUSB_SPEED_UNKNOWN, 0x0300, 400)]
    public void MaxPower_is_in_milliampere_for_the_speed_the_device_operates_at(
        libusb_speed speed,
        ushort bcdUsb,
        int expectedMaxPower
    )
    {
        var device = FakeHelper.CreateFakeDevice(
            VendorA,
            ProductA1,
            BusNumber,
            busAddress: 1,
            bcdUsb: bcdUsb
        );
        A.CallTo(() => device.GetSpeed()).Returns(speed);
        A.CallTo(() => device.GetActiveConfigDescriptor())
            .Returns(
                new libusb_config_descriptor(
                    bLength: 9,
                    libusb_descriptor_type.LIBUSB_DT_CONFIG,
                    wTotalLength: 9,
                    bNumInterfaces: 0,
                    bConfigurationValue: 1,
                    iConfiguration: 0,
                    libusb_config_desc_attributes.NONE,
                    bMaxPower: 50,
                    interfaces: [],
                    extra: []
                )
            );
        A.CallTo(() => device.Open()).Returns(A.Fake<ISafeDeviceHandle>());
        using var usb = CreateInitializedUsb(device);
        var deviceKey = usb.GetDeviceList().Single().DeviceKey;

        using var openDevice = usb.OpenDevice(deviceKey);

        openDevice.ConfigDescriptor.MaxPower.Should().Be(expectedMaxPower);
    }

    /// <summary>
    /// Make the fake device return the given value for every operating system device string.
    /// </summary>
    private static void FakeOsDeviceString(ISafeDevice device, string value)
    {
        string? ignoredValue;
        libusb_error? ignoredError;
        A.CallTo(() =>
                device.TryGetDeviceString(
                    A<libusb_device_string_type>._,
                    out ignoredValue,
                    out ignoredError
                )
            )
            .Returns(true)
            .AssignsOutAndRefParameters(value, null);
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
