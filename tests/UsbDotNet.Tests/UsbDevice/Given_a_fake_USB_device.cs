using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.Transfer;

namespace UsbDotNet.Tests.UsbDevice;

public sealed class Given_a_fake_USB_device : IDisposable
{
    private readonly UsbDotNet.Usb _usb = new(A.Fake<ILibUsb>());

    public void Dispose() => _usb.Dispose();

    [Fact]
    public void String_getters_return_an_empty_string_when_the_device_has_no_string()
    {
        using var device = CreateFakeDevice(
            out var handle,
            manufacturerIndex: 0,
            productIndex: 0,
            serialNumberIndex: 0
        );

        using var scope = new AssertionScope();
        device.GetManufacturer().Should().BeEmpty();
        device.GetProduct().Should().BeEmpty();
        device.GetSerialNumber().Should().BeEmpty();
        A.CallTo(() => handle.GetStringDescriptorAscii(A<byte>._)).MustNotHaveHappened();
    }

    [Fact]
    public void ReadStringDescriptor_returns_an_empty_string_for_index_zero()
    {
        using var device = CreateFakeDevice(out var handle);

        device.ReadStringDescriptor(0).Should().BeEmpty();

        A.CallTo(() => handle.GetStringDescriptorAscii(A<byte>._)).MustNotHaveHappened();
    }

    [Fact]
    public void String_getters_read_the_string_at_the_index_of_the_device_descriptor()
    {
        using var device = CreateFakeDevice(
            out var handle,
            manufacturerIndex: 1,
            productIndex: 0,
            serialNumberIndex: 3
        );
        A.CallTo(() => handle.GetStringDescriptorAscii(1)).Returns("Manufacturer");
        A.CallTo(() => handle.GetStringDescriptorAscii(3)).Returns("Serial");

        using var scope = new AssertionScope();
        device.GetManufacturer().Should().Be("Manufacturer");
        device.GetProduct().Should().BeEmpty();
        device.GetSerialNumber().Should().Be("Serial");
        device.ReadStringDescriptor(3).Should().Be("Serial");
    }

    [Fact]
    public void String_getters_throw_for_a_device_without_strings_when_the_device_is_disposed()
    {
        var device = CreateFakeDevice(
            out _,
            manufacturerIndex: 0,
            productIndex: 0,
            serialNumberIndex: 0
        );
        device.Dispose();

        var getSerialNumber = () => device.GetSerialNumber();
        var readStringDescriptor = () => device.ReadStringDescriptor(0);

        getSerialNumber.Should().Throw<ObjectDisposedException>();
        readStringDescriptor.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ControlRead_returns_Interrupted_when_the_device_is_disposed()
    {
        var device = CreateFakeDevice(out var handle);
        device.Dispose();

        var result = device.ControlRead(
            new byte[8],
            out var bytesRead,
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            request: 0x06,
            value: 0x0100,
            index: 0,
            timeout: 100
        );

        result.Should().Be(UsbResult.Interrupted);
        bytesRead.Should().Be(0);
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void ControlWrite_returns_Interrupted_when_the_device_is_disposed()
    {
        var device = CreateFakeDevice(out var handle);
        device.Dispose();

        var result = device.ControlWrite(
            new byte[8],
            out var bytesWritten,
            ControlRequestRecipient.Device,
            ControlRequestType.Vendor,
            request: 0x01,
            value: 0,
            index: 0,
            timeout: 100
        );

        result.Should().Be(UsbResult.Interrupted);
        bytesWritten.Should().Be(0);
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void Control_transfers_still_throw_for_an_invalid_buffer_when_the_device_is_disposed()
    {
        var device = CreateFakeDevice(out _);
        device.Dispose();
        var tooLarge = new byte[ushort.MaxValue + 1];

        var read = () =>
            device.ControlRead(
                tooLarge,
                out _,
                ControlRequestRecipient.Device,
                ControlRequestType.Standard,
                request: 0x06,
                value: 0,
                index: 0,
                timeout: 100
            );
        var write = () =>
            device.ControlWrite(
                tooLarge,
                out _,
                ControlRequestRecipient.Device,
                ControlRequestType.Standard,
                request: 0x06,
                value: 0,
                index: 0,
                timeout: 100
            );

        read.Should().Throw<ArgumentOutOfRangeException>();
        write.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(int.MinValue)]
    public void Control_transfers_throw_for_an_invalid_timeout(int timeout)
    {
        using var device = CreateFakeDevice(out var handle);

        var read = () =>
            device.ControlRead(
                new byte[8],
                out _,
                ControlRequestRecipient.Device,
                ControlRequestType.Standard,
                request: 0x06,
                value: 0x0100,
                index: 0,
                timeout
            );
        var write = () =>
            device.ControlWrite(
                new byte[8],
                out _,
                ControlRequestRecipient.Device,
                ControlRequestType.Vendor,
                request: 0x01,
                value: 0,
                index: 0,
                timeout
            );

        read.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(timeout));
        write.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(timeout));
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Control_transfers_accept_a_valid_timeout(int timeout)
    {
        // A disposed device returns Interrupted once the arguments are validated
        var device = CreateFakeDevice(out _);
        device.Dispose();

        var read = device.ControlRead(
            new byte[8],
            out _,
            ControlRequestRecipient.Device,
            ControlRequestType.Standard,
            request: 0x06,
            value: 0x0100,
            index: 0,
            timeout
        );
        var write = device.ControlWrite(
            new byte[8],
            out _,
            ControlRequestRecipient.Device,
            ControlRequestType.Vendor,
            request: 0x01,
            value: 0,
            index: 0,
            timeout
        );

        read.Should().Be(UsbResult.Interrupted);
        write.Should().Be(UsbResult.Interrupted);
    }

    private UsbDotNet.UsbDevice CreateFakeDevice(
        out ISafeDeviceHandle handle,
        byte manufacturerIndex = 1,
        byte productIndex = 2,
        byte serialNumberIndex = 3
    )
    {
        handle = A.Fake<ISafeDeviceHandle>();
        return new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor
            {
                DeviceKey = "FAKE_0000_0_0",
                ManufacturerIndex = manufacturerIndex,
                ProductIndex = productIndex,
                SerialNumberIndex = serialNumberIndex,
            },
            A.Fake<IUsbConfigDescriptor>()
        );
    }
}
