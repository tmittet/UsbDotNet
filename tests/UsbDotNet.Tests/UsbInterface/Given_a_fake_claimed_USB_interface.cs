using System.Runtime.InteropServices;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.Tests.UsbInterface;

public sealed class Given_a_fake_claimed_USB_interface : IDisposable
{
    private const int ReaderCount = 8;
    private const int Iterations = 20;

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly UsbDotNet.Usb _usb = new(A.Fake<ILibUsb>());

    public void Dispose() => _usb.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(511)]
    [InlineData(513)]
    [InlineData(1000)]
    public void BulkRead_throws_when_buffer_length_is_not_a_non_zero_multiple_of_the_max_packet_size(
        int length
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(device, handle, inputMaxPacketSize: 512);
        var buffer = new byte[length];

        var act = () => usbInterface.BulkRead(buffer, out _, 100);

        act.Should().Throw<ArgumentException>().WithMessage("*multiple of the endpoint*(512)*");
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(32 * 1024)]
    [InlineData(64 * 1024)]
    public void BulkRead_does_not_throw_when_buffer_length_is_a_multiple_of_the_max_packet_size(
        int length
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(device, handle, inputMaxPacketSize: 512);
        var buffer = new byte[length];

        var act = () => usbInterface.BulkRead(buffer, out _, 100);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(512, 512, 512)]
    [InlineData(512, 32 * 1024, 32 * 1024)]
    [InlineData(512, 64 * 1024, 32 * 1024)]
    [InlineData(1000, 3000, 3000)]
    [InlineData(1000, 32000, 32000)]
    [InlineData(1000, 33000, 32000)]
    [InlineData(1000, 64000, 32000)]
    public void BulkRead_submits_a_transfer_length_that_is_capped_to_a_multiple_of_the_max_packet_size(
        ushort maxPacketSize,
        int length,
        int expectedTransferLength
    )
    {
        var transferLength = BulkReadAndCaptureTransferLength(maxPacketSize, length);

        transferLength.Should().Be(expectedTransferLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(1000)]
    public void BulkRead_does_not_check_the_buffer_length_when_the_max_packet_size_is_unknown(
        int length
    )
    {
        var transferLength = BulkReadAndCaptureTransferLength(maxPacketSize: 0, length);

        transferLength.Should().Be(length);
    }

    /// <summary>
    /// Do a bulk read on a fake interface and return the length of the transfer that was
    /// submitted to libusb, or null when no transfer was submitted.
    /// </summary>
    private int? BulkReadAndCaptureTransferLength(ushort maxPacketSize, int length)
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(device, handle, maxPacketSize);
        // BulkRead writes the libusb transfer struct to this memory before it is submitted
        var transferPtr = Marshal.AllocHGlobal(Marshal.SizeOf<libusb_transfer>());
        try
        {
            int? transferLength = null;
            var transfer = A.Fake<ISafeTransfer>();
            A.CallTo(() => handle.AllocateTransfer(A<int>._)).Returns(transfer);
            A.CallTo(() => transfer.GetBufferPtr()).Returns(transferPtr);
            // Fail the submit, so that the read returns without waiting for a completion
            A.CallTo(() => transfer.Submit())
                .Invokes(() =>
                    transferLength = Marshal.PtrToStructure<libusb_transfer>(transferPtr).length
                )
                .Returns(libusb_error.LIBUSB_ERROR_NO_DEVICE);

            _ = usbInterface.BulkRead(new byte[length], out _, 100);

            return transferLength;
        }
        finally
        {
            Marshal.FreeHGlobal(transferPtr);
        }
    }

    private UsbDotNet.UsbDevice CreateFakeDevice(out ISafeDeviceHandle handle)
    {
        handle = A.Fake<ISafeDeviceHandle>();
        return new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor { DeviceKey = "FAKE_0000_0_0" },
            A.Fake<IUsbConfigDescriptor>()
        );
    }

    private static IUsbInterface ClaimFakeInterface(
        UsbDotNet.UsbDevice device,
        ISafeDeviceHandle handle,
        ushort inputMaxPacketSize
    )
    {
        A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(A.Fake<ISafeDeviceInterface>());
        var inputEndpoint = FakeEndpoint(0x81, UsbEndpointTransferType.Bulk);
        A.CallTo(() => inputEndpoint.MaxPacketSize).Returns(inputMaxPacketSize);
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.Endpoints).Returns([inputEndpoint]);
        return device.ClaimInterface(descriptor);
    }

    private static IUsbEndpointDescriptor FakeEndpoint(
        byte address,
        UsbEndpointTransferType transferType
    )
    {
        var endpoint = A.Fake<IUsbEndpointDescriptor>();
        A.CallTo(() => endpoint.EndpointAddress).Returns(new UsbEndpointAddress(address));
        A.CallTo(() => endpoint.Attributes).Returns(new UsbEndpointAttributes((byte)transferType));
        return endpoint;
    }

    private static IUsbInterface ClaimFakeInterface(
        UsbDotNet.UsbDevice device,
        ISafeDeviceHandle handle,
        params IUsbEndpointDescriptor[] endpoints
    )
    {
        A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(A.Fake<ISafeDeviceInterface>());
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.Endpoints).Returns(endpoints);
        return device.ClaimInterface(descriptor);
    }

    [Fact]
    public void Bulk_endpoints_are_selected_over_earlier_endpoints_of_another_transfer_type()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(
            device,
            handle,
            FakeEndpoint(0x81, UsbEndpointTransferType.Interrupt),
            FakeEndpoint(0x02, UsbEndpointTransferType.Isochronous),
            FakeEndpoint(0x83, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x04, UsbEndpointTransferType.Bulk)
        );

        usbInterface
            .TryGetInputEndpoint(out var input, UsbEndpointTransferType.Bulk)
            .Should()
            .BeTrue();
        usbInterface
            .TryGetOutputEndpoint(out var output, UsbEndpointTransferType.Bulk)
            .Should()
            .BeTrue();

        input!.EndpointAddress.RawValue.Should().Be(0x83);
        output!.EndpointAddress.RawValue.Should().Be(0x04);
    }

    [Fact]
    public void Endpoints_of_another_transfer_type_are_found_when_the_type_is_specified()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(
            device,
            handle,
            FakeEndpoint(0x81, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x02, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x83, UsbEndpointTransferType.Interrupt),
            FakeEndpoint(0x04, UsbEndpointTransferType.Interrupt)
        );

        usbInterface
            .TryGetInputEndpoint(out var input, UsbEndpointTransferType.Interrupt)
            .Should()
            .BeTrue();
        usbInterface
            .TryGetOutputEndpoint(out var output, UsbEndpointTransferType.Interrupt)
            .Should()
            .BeTrue();

        input!.EndpointAddress.RawValue.Should().Be(0x83);
        output!.EndpointAddress.RawValue.Should().Be(0x04);
        usbInterface
            .TryGetInputEndpoint(out _, UsbEndpointTransferType.Isochronous)
            .Should()
            .BeFalse();
        usbInterface
            .TryGetOutputEndpoint(out _, UsbEndpointTransferType.Control)
            .Should()
            .BeFalse();
    }

    [Theory]
    // Read endpoint: wrong transfer type, then wrong direction
    [InlineData(0x81, UsbEndpointTransferType.Interrupt, true, "readEndpoint")]
    [InlineData(0x81, UsbEndpointTransferType.Isochronous, true, "readEndpoint")]
    [InlineData(0x01, UsbEndpointTransferType.Bulk, true, "readEndpoint")]
    // Write endpoint: wrong transfer type, then wrong direction
    [InlineData(0x01, UsbEndpointTransferType.Interrupt, false, "writeEndpoint")]
    [InlineData(0x81, UsbEndpointTransferType.Bulk, false, "writeEndpoint")]
    public void Constructor_throws_when_an_explicit_endpoint_is_not_a_bulk_endpoint_in_that_direction(
        byte address,
        UsbEndpointTransferType transferType,
        bool isReadEndpoint,
        string expectedParameterName
    )
    {
        using var device = CreateFakeDevice(out _);
        var endpoint = FakeEndpoint(address, transferType);

        var act = () =>
            new UsbDotNet.UsbInterface(
                NullLogger<UsbDotNet.UsbInterface>.Instance,
                device,
                A.Fake<IUsbInterfaceDescriptor>(),
                A.Fake<ISafeDeviceInterface>(),
                readEndpoint: isReadEndpoint ? endpoint : null,
                writeEndpoint: isReadEndpoint ? null : endpoint
            );

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*must be a bulk*")
            .WithParameterName(expectedParameterName);
    }

    [Fact]
    public void Bulk_transfers_use_the_explicit_bulk_endpoints_from_the_constructor()
    {
        using var device = CreateFakeDevice(out _);
        var readEndpoint = FakeEndpoint(0x85, UsbEndpointTransferType.Bulk);
        var writeEndpoint = FakeEndpoint(0x06, UsbEndpointTransferType.Bulk);

        // Not disposed: the interface was not claimed through the device, so the device
        // does not know it and Dispose would throw when asking the device to release it.
#pragma warning disable CA2000 // Dispose objects before losing scope
        var usbInterface = new UsbDotNet.UsbInterface(
            NullLogger<UsbDotNet.UsbInterface>.Instance,
            device,
            A.Fake<IUsbInterfaceDescriptor>(),
            A.Fake<ISafeDeviceInterface>(),
            readEndpoint,
            writeEndpoint
        );
#pragma warning restore CA2000

        usbInterface.TryGetInputEndpoint(out var input, UsbEndpointTransferType.Bulk);
        usbInterface.TryGetOutputEndpoint(out var output, UsbEndpointTransferType.Bulk);
        input.Should().BeSameAs(readEndpoint);
        output.Should().BeSameAs(writeEndpoint);
    }

    [Fact]
    public void Bulk_transfers_throw_when_the_interface_has_no_bulk_endpoint()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterface(
            device,
            handle,
            FakeEndpoint(0x81, UsbEndpointTransferType.Interrupt),
            FakeEndpoint(0x02, UsbEndpointTransferType.Interrupt)
        );

        var read = () => usbInterface.BulkRead(new byte[64], out _, 100);
        var write = () => usbInterface.BulkWrite(new byte[64], out _, 100);

        usbInterface.TryGetInputEndpoint(out _, UsbEndpointTransferType.Bulk).Should().BeFalse();
        usbInterface.TryGetOutputEndpoint(out _, UsbEndpointTransferType.Bulk).Should().BeFalse();
        read.Should().Throw<InvalidOperationException>().WithMessage("*no bulk Input endpoint*");
        write.Should().Throw<InvalidOperationException>().WithMessage("*no bulk Output endpoint*");
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(0x83, libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_INTERRUPT)]
    // The second bulk input endpoint; not the one that BulkRead selects
    [InlineData(0x85, libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK)]
    public void Read_submits_a_transfer_of_the_endpoint_transfer_type_to_the_given_endpoint(
        byte address,
        libusb_endpoint_transfer_type expectedType
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        // The endpoint is identified by its address, it need not be the same descriptor instance
        var endpoint = FakeEndpoint(address, UsbEndpointTransferType.Control);

        var transfer = CaptureSubmittedTransfer(
            handle,
            () => usbInterface.Read(endpoint, new byte[64], out _, 100)
        );

        transfer.Should().NotBeNull();
        transfer!.Value.endpoint.Should().Be(address);
        transfer.Value.type.Should().Be(expectedType);
        transfer.Value.length.Should().Be(64);
    }

    [Theory]
    [InlineData(0x04, libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_INTERRUPT)]
    // The second bulk output endpoint; not the one that BulkWrite selects
    [InlineData(0x06, libusb_endpoint_transfer_type.LIBUSB_ENDPOINT_TRANSFER_TYPE_BULK)]
    public void Write_submits_a_transfer_of_the_endpoint_transfer_type_to_the_given_endpoint(
        byte address,
        libusb_endpoint_transfer_type expectedType
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        // The endpoint is identified by its address, it need not be the same descriptor instance
        var endpoint = FakeEndpoint(address, UsbEndpointTransferType.Control);

        var transfer = CaptureSubmittedTransfer(
            handle,
            () => usbInterface.Write(endpoint, new byte[10], out _, 100)
        );

        transfer.Should().NotBeNull();
        transfer!.Value.endpoint.Should().Be(address);
        transfer.Value.type.Should().Be(expectedType);
        transfer.Value.length.Should().Be(10);
    }

    [Fact]
    public void BulkRead_and_BulkWrite_submit_to_the_first_bulk_endpoints()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);

        var read = CaptureSubmittedTransfer(
            handle,
            () => usbInterface.BulkRead(new byte[64], out _, 100)
        );
        var write = CaptureSubmittedTransfer(
            handle,
            () => usbInterface.BulkWrite(new byte[10], out _, 100)
        );

        read!.Value.endpoint.Should().Be(0x81);
        write!.Value.endpoint.Should().Be(0x02);
    }

    [Theory]
    // An output endpoint of the interface
    [InlineData(0x02, "*must be an Input endpoint*")]
    // An input endpoint that the interface does not have
    [InlineData(0x8F, "*not an endpoint of interface*")]
    public void Read_throws_when_the_endpoint_is_not_an_input_endpoint_of_the_interface(
        byte address,
        string expectedMessage
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        var endpoint = FakeEndpoint(address, UsbEndpointTransferType.Bulk);

        var act = () => usbInterface.Read(endpoint, new byte[64], out _, 100);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(expectedMessage)
            .WithParameterName("endpoint");
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Theory]
    // An input endpoint of the interface
    [InlineData(0x81, "*must be an Output endpoint*")]
    // An output endpoint that the interface does not have
    [InlineData(0x0F, "*not an endpoint of interface*")]
    public void Write_throws_when_the_endpoint_is_not_an_output_endpoint_of_the_interface(
        byte address,
        string expectedMessage
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        var endpoint = FakeEndpoint(address, UsbEndpointTransferType.Bulk);

        var act = () => usbInterface.Write(endpoint, new byte[10], out _, 100);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(expectedMessage)
            .WithParameterName("endpoint");
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void Read_and_Write_throw_when_the_endpoint_is_isochronous()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);

        var read = () =>
            usbInterface.Read(
                FakeEndpoint(0x87, UsbEndpointTransferType.Isochronous),
                new byte[64],
                out _,
                100
            );
        var write = () =>
            usbInterface.Write(
                FakeEndpoint(0x08, UsbEndpointTransferType.Isochronous),
                new byte[10],
                out _,
                100
            );

        read.Should().Throw<NotSupportedException>().WithMessage("Isochronous*");
        write.Should().Throw<NotSupportedException>().WithMessage("Isochronous*");
        A.CallTo(() => handle.AllocateTransfer(A<int>._)).MustNotHaveHappened();
    }

    [Fact]
    public void Read_and_Write_throw_when_the_endpoint_is_null()
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);

        var read = () => usbInterface.Read(null!, new byte[64], out _, 100);
        var write = () => usbInterface.Write(null!, new byte[10], out _, 100);

        read.Should().Throw<ArgumentNullException>().WithParameterName("endpoint");
        write.Should().Throw<ArgumentNullException>().WithParameterName("endpoint");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(100)]
    public void Read_throws_when_buffer_length_is_not_a_non_zero_multiple_of_the_max_packet_size(
        int length
    )
    {
        using var device = CreateFakeDevice(out var handle);
        using var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        usbInterface.TryGetInputEndpoint(out var endpoint, UsbEndpointTransferType.Interrupt);

        var act = () => usbInterface.Read(endpoint!, new byte[length], out _, 100);

        act.Should().Throw<ArgumentException>().WithMessage("*multiple of the endpoint*(64)*");
    }

    [Theory]
    // wMaxPacketSize 0x1400: 1024 byte packets, with 2 additional transactions per microframe
    [InlineData(0x1400, 1024)]
    [InlineData(0x1400, 3072)]
    [InlineData(0x1400, 5120)]
    // wMaxPacketSize 0x0C00: 1024 byte packets, with 1 additional transaction per microframe
    [InlineData(0x0C00, 2048)]
    public void Read_ignores_the_additional_transaction_bits_of_a_high_bandwidth_max_packet_size(
        ushort maxPacketSize,
        int length
    )
    {
        using var device = CreateFakeDevice(out var handle);
        var endpoint = HighBandwidthInterruptInputEndpoint(maxPacketSize);
        using var usbInterface = ClaimFakeInterface(device, handle, endpoint);

        var transfer = CaptureSubmittedTransfer(
            handle,
            () => usbInterface.Read(endpoint, new byte[length], out _, 100)
        );

        transfer.Should().NotBeNull();
        transfer!.Value.length.Should().Be(length);
    }

    [Fact]
    public void Read_checks_the_buffer_length_against_the_packet_size_of_a_high_bandwidth_endpoint()
    {
        using var device = CreateFakeDevice(out var handle);
        var endpoint = HighBandwidthInterruptInputEndpoint(0x1400);
        using var usbInterface = ClaimFakeInterface(device, handle, endpoint);

        var act = () => usbInterface.Read(endpoint, new byte[1000], out _, 100);

        act.Should().Throw<ArgumentException>().WithMessage("*multiple of the endpoint*(1024)*");
    }

    /// <summary>
    /// A real endpoint descriptor, with the raw wMaxPacketSize value of the USB descriptor.
    /// </summary>
    private static IUsbEndpointDescriptor HighBandwidthInterruptInputEndpoint(
        ushort maxPacketSizeRawValue
    ) =>
        new UsbEndpointDescriptor(
            new UsbEndpointAddress(0x81),
            new UsbEndpointAttributes((byte)UsbEndpointTransferType.Interrupt),
            maxPacketSizeRawValue,
            Interval: 1,
            Refresh: 0,
            SynchAddress: 0,
            ExtraBytes: []
        );

    [Fact]
    public void Read_and_Write_return_Interrupted_when_the_interface_is_disposed()
    {
        using var device = CreateFakeDevice(out var handle);
        var usbInterface = ClaimFakeInterfaceWithSeveralEndpoints(device, handle);
        usbInterface.TryGetInputEndpoint(out var input, UsbEndpointTransferType.Interrupt);
        usbInterface.TryGetOutputEndpoint(out var output, UsbEndpointTransferType.Interrupt);
        usbInterface.Dispose();

        var readResult = usbInterface.Read(input!, new byte[64], out var bytesRead, 100);
        var writeResult = usbInterface.Write(output!, new byte[10], out var bytesWritten, 100);

        readResult.Should().Be(UsbResult.Interrupted);
        writeResult.Should().Be(UsbResult.Interrupted);
        bytesRead.Should().Be(0);
        bytesWritten.Should().Be(0);
    }

    /// <summary>
    /// Claim a fake interface with two bulk, one interrupt and one isochronous endpoint in each
    /// direction; all with a max packet size of 64 bytes.
    /// </summary>
    private static IUsbInterface ClaimFakeInterfaceWithSeveralEndpoints(
        UsbDotNet.UsbDevice device,
        ISafeDeviceHandle handle
    )
    {
        IUsbEndpointDescriptor[] endpoints =
        [
            FakeEndpoint(0x81, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x02, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x83, UsbEndpointTransferType.Interrupt),
            FakeEndpoint(0x04, UsbEndpointTransferType.Interrupt),
            FakeEndpoint(0x85, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x06, UsbEndpointTransferType.Bulk),
            FakeEndpoint(0x87, UsbEndpointTransferType.Isochronous),
            FakeEndpoint(0x08, UsbEndpointTransferType.Isochronous),
        ];
        foreach (var endpoint in endpoints)
        {
            A.CallTo(() => endpoint.MaxPacketSize).Returns((ushort)64);
        }
        return ClaimFakeInterface(device, handle, endpoints);
    }

    /// <summary>
    /// Run a transfer on a fake interface and return the libusb transfer that was submitted,
    /// or null when no transfer was submitted. The submit fails, so the transfer does not wait.
    /// </summary>
    private static libusb_transfer? CaptureSubmittedTransfer(
        ISafeDeviceHandle handle,
        Func<UsbResult> transfer
    )
    {
        // The libusb transfer struct is written to this memory before it is submitted
        var transferPtr = Marshal.AllocHGlobal(Marshal.SizeOf<libusb_transfer>());
        try
        {
            libusb_transfer? submitted = null;
            var safeTransfer = A.Fake<ISafeTransfer>();
            A.CallTo(() => handle.AllocateTransfer(A<int>._)).Returns(safeTransfer);
            A.CallTo(() => safeTransfer.GetBufferPtr()).Returns(transferPtr);
            A.CallTo(() => safeTransfer.Submit())
                .Invokes(() => submitted = Marshal.PtrToStructure<libusb_transfer>(transferPtr))
                .Returns(libusb_error.LIBUSB_ERROR_NO_DEVICE);

            transfer().Should().Be(UsbResult.NoDevice);

            return submitted;
        }
        finally
        {
            Marshal.FreeHGlobal(transferPtr);
        }
    }

    [Fact]
    public void ClaimInterface_failure_message_names_the_interface_and_the_device()
    {
        using var device = CreateFakeDevice(out var handle);
        A.CallTo(() => handle.ClaimInterface(A<byte>._))
            .Throws(new LibUsbException(libusb_error.LIBUSB_ERROR_BUSY, "Native claim failed."));
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.InterfaceNumber).Returns((byte)3);

        var act = () => device.ClaimInterface(descriptor);

        act.Should()
            .Throw<UsbException>()
            .WithMessage("Failed to claim interface 3 on device 'FAKE_0000_0_0'.")
            .Which.Code.Should()
            .Be(UsbResult.ResourceBusy);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ClaimInterface_selects_the_alternate_setting_of_the_descriptor(
        byte alternateSetting
    )
    {
        using var device = CreateFakeDevice(out var handle);
        var claimedInterface = A.Fake<ISafeDeviceInterface>();
        A.CallTo(() => handle.ClaimInterface(2)).Returns(claimedInterface);

        using var usbInterface = device.ClaimInterface(
            FakeInterfaceDescriptor(interfaceNumber: 2, alternateSetting)
        );

        usbInterface.Number.Should().Be(2);
        usbInterface.AlternateSetting.Should().Be(alternateSetting);
        A.CallTo(() => claimedInterface.SetAlternateSetting(alternateSetting))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void ClaimInterface_does_not_request_the_default_alternate_setting()
    {
        using var device = CreateFakeDevice(out var handle);
        var claimedInterface = A.Fake<ISafeDeviceInterface>();
        A.CallTo(() => handle.ClaimInterface(2)).Returns(claimedInterface);

        using var usbInterface = device.ClaimInterface(
            FakeInterfaceDescriptor(interfaceNumber: 2, alternateSetting: 0)
        );

        usbInterface.AlternateSetting.Should().Be(0);
        A.CallTo(() => claimedInterface.SetAlternateSetting(A<byte>._)).MustNotHaveHappened();
    }

    [Fact]
    public void ClaimInterface_releases_the_interface_when_the_alternate_setting_cannot_be_selected()
    {
        using var device = CreateFakeDevice(out var handle);
        var failingInterface = A.Fake<ISafeDeviceInterface>();
        A.CallTo(() => failingInterface.SetAlternateSetting(A<byte>._))
            .Throws(new LibUsbException(libusb_error.LIBUSB_ERROR_NOT_FOUND, "Native failure."));
        A.CallTo(() => handle.ClaimInterface(2))
            .Returns(failingInterface)
            .Once()
            .Then.Returns(A.Fake<ISafeDeviceInterface>());

        var act = () =>
            device.ClaimInterface(FakeInterfaceDescriptor(interfaceNumber: 2, alternateSetting: 1));

        act.Should()
            .Throw<UsbException>()
            .WithMessage(
                "Failed to select alternate setting 1 of interface 2 on device 'FAKE_0000_0_0'."
            )
            .Which.Code.Should()
            .Be(UsbResult.NotFound);
        A.CallTo(() => failingInterface.Dispose()).MustHaveHappenedOnceExactly();
        // The failed claim must not leave the interface registered as claimed
        using var usbInterface = device.ClaimInterface(
            FakeInterfaceDescriptor(interfaceNumber: 2, alternateSetting: 0)
        );
        usbInterface.Number.Should().Be(2);
    }

    private static IUsbInterfaceDescriptor FakeInterfaceDescriptor(
        byte interfaceNumber,
        byte alternateSetting
    )
    {
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.InterfaceNumber).Returns(interfaceNumber);
        A.CallTo(() => descriptor.AlternateSetting).Returns(alternateSetting);
        return descriptor;
    }

    [Fact]
    public void Dispose_does_not_throw_when_BulkReads_race_an_in_progress_dispose()
    {
        // Deterministic repro of the race behind the failure in
        // Disposing_the_USB_interface_cancels_an_ongoing_Huddly_device_transfer:
        // BulkRead threads enter while Dispose holds the dispose write lock, and
        // Dispose then tears the lock down. Dispose must neither throw
        // SynchronizationLockException nor deadlock with the racing readers, and
        // every racing BulkRead must report UsbResult.Interrupted.
        var handle = A.Fake<ISafeDeviceHandle>();
        using var device = new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor { DeviceKey = "FAKE_0000_0_0" },
            A.Fake<IUsbConfigDescriptor>()
        );
        // BulkRead resolves the bulk input endpoint before it checks for an in-progress dispose
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.Endpoints)
            .Returns([FakeEndpoint(0x81, UsbEndpointTransferType.Bulk)]);
        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var claimedInterface = A.Fake<ISafeDeviceInterface>();
            A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(claimedInterface);
            var usbInterface = device.ClaimInterface(descriptor);

            using var writeLockHeld = new ManualResetEventSlim();
            using var readersFinished = new CountdownEvent(ReaderCount);
            var allReadersFinishedWhileDisposing = false;
            // The claimed interface is disposed on the disposing thread while
            // UsbInterface.Dispose() holds the write lock. Release the readers here and
            // keep holding the write lock until every reader's BulkRead has returned,
            // proving that all readers observed the in-progress dispose.
            A.CallTo(() => claimedInterface.Dispose())
                .Invokes(() =>
                {
                    writeLockHeld.Set();
                    allReadersFinishedWhileDisposing = readersFinished.Wait(WaitTimeout);
                });

            var readResults = new UsbResult[ReaderCount];
            var readerThreads = Enumerable
                .Range(0, ReaderCount)
                .Select(reader =>
                {
                    var thread = new Thread(() =>
                    {
                        if (!writeLockHeld.Wait(WaitTimeout))
                        {
                            readResults[reader] = UsbResult.Timeout;
                            return;
                        }
                        var buffer = new byte[16];
                        readResults[reader] = usbInterface.BulkRead(
                            buffer,
                            out _,
                            Timeout.Infinite
                        );
                        readersFinished.Signal();
                    })
                    {
                        IsBackground = true,
                    };
                    return thread;
                })
                .ToArray();
            foreach (var readerThread in readerThreads)
            {
                readerThread.Start();
            }

            var dispose = () => usbInterface.Dispose();
            dispose.Should().NotThrow($"dispose failed on iteration {iteration}");
            allReadersFinishedWhileDisposing
                .Should()
                .BeTrue("all BulkRead threads should have completed during the dispose");
            foreach (var readerThread in readerThreads)
            {
                readerThread.Join(WaitTimeout).Should().BeTrue("readers should not hang");
            }
            readResults.Should().AllBeEquivalentTo(UsbResult.Interrupted);
        }
    }

    [Fact]
    public async Task Device_dispose_does_not_deadlock_with_a_concurrent_interface_dispose()
    {
        // Deterministic repro of a lock-order inversion: UsbDevice.Dispose() must not
        // hold its interface lock while calling UsbInterface.Dispose(), because that
        // dispose holds its own dispose monitor while calling back into
        // UsbDevice.ReleaseInterface(), which takes the interface lock.
        var handle = A.Fake<ISafeDeviceHandle>();
#pragma warning disable CA2000 // Dispose objects before losing scope; disposed by the deviceDispose task below
        var device = new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor { DeviceKey = "FAKE_0000_0_0" },
            A.Fake<IUsbConfigDescriptor>()
        );
#pragma warning restore CA2000
        var claimedInterface = A.Fake<ISafeDeviceInterface>();
        A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(claimedInterface);

        var inputEndpoint = FakeEndpoint(0x81, UsbEndpointTransferType.Bulk);

        // BulkRead reads the endpoint max packet size inside the dispose read lock. Park
        // the reader there, so the interface dispose stays in EnterWriteLock, holding its
        // dispose monitor, until the reader is released.
        using var readerInsideDisposeLock = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        A.CallTo(() => inputEndpoint.MaxPacketSize)
            .ReturnsLazily(() =>
            {
                readerInsideDisposeLock.Set();
                releaseReader.Wait(WaitTimeout);
                return (ushort)0;
            });
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.Endpoints).Returns([inputEndpoint]);
        var usbInterface = device.ClaimInterface(descriptor);

        var readResult = UsbResult.UnknownError;
        var readerThread = new Thread(() =>
        {
            var buffer = new byte[16];
            readResult = usbInterface.BulkRead(buffer, out _, Timeout.Infinite);
        })
        {
            IsBackground = true,
        };
        readerThread.Start();
        readerInsideDisposeLock
            .Wait(WaitTimeout)
            .Should()
            .BeTrue("the reader should be parked inside the dispose read lock");

        var interfaceDispose = Task.Run(usbInterface.Dispose);
        // Give the interface dispose time to take its dispose monitor and begin
        // waiting for the parked reader to drain, before the device dispose starts
        await Task.Delay(50);
        var deviceDispose = Task.Run(device.Dispose);
        // Give the device dispose time to reach the interface disposal, then unpark
        // the reader so the interface dispose proceeds into ReleaseInterface()
        await Task.Delay(50);
        releaseReader.Set();

        // On deadlock the dispose tasks never complete
        await Task.WhenAll(interfaceDispose, deviceDispose).WaitAsync(WaitTimeout);
        readerThread.Join(WaitTimeout).Should().BeTrue("the reader should not hang");
        readResult.Should().Be(UsbResult.Interrupted);
    }
}
