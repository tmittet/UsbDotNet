using System.Collections.Concurrent;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Tests.Fakes;

namespace UsbDotNet.Tests.Usb;

[Trait("Category", "UsbDevice")]
public sealed class Given_any_USB_device : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_any_USB_device> _logger;
    private readonly UsbDotNet.Usb _usb;
    private readonly TestDeviceSource _deviceSource;

    public Given_any_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_any_USB_device>();
        _usb = new UsbDotNet.Usb(
            _libusb,
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.Information }
        );
        try
        {
            _usb.Initialize();
            _deviceSource = new TestDeviceSource(_logger, _usb);
            _deviceSource.SetPreferredVendorId(0x2BD9);
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [SkippableFact]
    public void GetDeviceList_returns_at_least_one_USB_device()
    {
        var descriptors = _usb.GetDeviceList();
        Skip.If(descriptors.Count == 0, "No USB device available.");

        descriptors.Should().HaveCountGreaterThanOrEqualTo(1);
        foreach (var descriptor in descriptors)
        {
            _logger.LogInformation(
                "Device found: Class={DeviceClass}, VID=0x{VID:X4}, PID=0x{PID:X4}, "
                    + "BusNumber={BusNumber}, BusAddress={BusAddress}, PortNumber={PortNumber}.",
                descriptor.DeviceClass,
                descriptor.VendorId,
                descriptor.ProductId,
                descriptor.BusNumber,
                descriptor.BusAddress,
                descriptor.PortNumber
            );
        }
    }

    [SkippableFact]
    public void OpenDevice_throws_UsbException_given_invalid_device_key()
    {
        var invalidDeviceKey = UsbDeviceDescriptor.GetKey(0xFFFF, 0xFFFF, 255, 255);
        var act = () => _usb.OpenDevice(invalidDeviceKey);
        act.Should()
            .Throw<UsbException>()
            .WithMessage("Failed to get device from list; the device could not be found.");
    }

    [SkippableFact]
    public void OpenDevice_throws_InvalidOperationException_when_device_is_already_open()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;
        var act = () => _usb.OpenDevice(deviceDescriptor);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{deviceDescriptor.DeviceKey}' already open.");
    }

    [SkippableFact]
    public void OpenDevice_is_able_to_find_device_based_on_device_key()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;

        // This is expected to throw InvalidOperationException; since the device is already open.
        // The test proves OpenDevice finds the device; another exception type with a different
        // error message would be thrown if the device key was invalid or device was not found.
        var act = () => _usb.OpenDevice(deviceDescriptor.DeviceKey);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{deviceDescriptor.DeviceKey}' already open.");
    }

    [SkippableFact]
    public void OpenDevice_is_able_to_find_device_based_on_VID_PID_bus_number_and_address()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        var deviceDescriptor = device.Descriptor;
        var validDeviceKey = UsbDeviceDescriptor.GetKey(
            deviceDescriptor.VendorId,
            deviceDescriptor.ProductId,
            deviceDescriptor.BusNumber,
            deviceDescriptor.BusAddress
        );

        // This is expected to throw InvalidOperationException; since the device is already open.
        // The test proves OpenDevice finds the device; another exception type with a different
        // error message would be thrown if the device key was invalid or device was not found.
        var act = () => _usb.OpenDevice(validDeviceKey);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"Device '{validDeviceKey}' already open.");
    }

    [SkippableFact]
    public void GetDeviceManufacturer_from_OS_matches_value_read_from_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Read the string descriptor directly from the open device
        var fromDevice = device.GetManufacturer();
        // Read the same value via the Usb type, which reads it from the operating system
        var fromOs = _usb.GetDeviceManufacturer(device.Descriptor);

        _logger.LogInformation(
            "Manufacturer: OS='{FromOs}', Device='{FromDevice}'.",
            fromOs,
            fromDevice
        );
        fromOs.Should().Be(fromDevice);
    }

    [SkippableFact]
    public void GetDeviceProduct_returns_product_name_given_a_device_descriptor_when_device_is_not_open()
    {
        IUsbDeviceDescriptor deviceDescriptor;
        using (var device = _deviceSource.OpenUsbDeviceOrSkip())
        {
            deviceDescriptor = device.Descriptor;
        }
        var productName = _usb.GetDeviceProduct(deviceDescriptor);
        productName.Should().NotBeNullOrWhiteSpace();
        productName.Should().NotEndWith("\0", because: "null terminator should be trimmed");
    }

    [SkippableFact]
    public void GetDeviceProduct_from_OS_matches_value_read_from_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Read the string descriptor directly from the open device
        var fromDevice = device.GetProduct();
        // Read the same value via the Usb type, which reads it from the operating system
        var fromOs = _usb.GetDeviceProduct(device.Descriptor);

        _logger.LogInformation(
            "Product: OS='{FromOs}', Device='{FromDevice}'.",
            fromOs,
            fromDevice
        );
        fromOs.Should().Be(fromDevice);
    }

    [SkippableFact]
    public void GetDeviceSerial_returns_serial_given_a_device_descriptor_when_device_is_not_open()
    {
        IUsbDeviceDescriptor deviceDescriptor;
        using (var device = _deviceSource.OpenUsbDeviceOrSkip())
        {
            deviceDescriptor = device.Descriptor;
        }
        var serial = _usb.GetDeviceSerial(deviceDescriptor);
        serial.Should().NotBeNullOrWhiteSpace();
        serial.Should().NotEndWith("\0", because: "null terminator should be trimmed");
    }

    [SkippableFact]
    public void GetDeviceSerial_from_OS_matches_value_read_from_open_device()
    {
        using var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Read the string descriptor directly from the open device
        var fromDevice = device.GetSerialNumber();
        // Read the same value via the Usb type, which reads it from the operating system
        var fromOs = _usb.GetDeviceSerial(device.Descriptor);

        _logger.LogInformation("Serial: OS='{FromOs}', Device='{FromDevice}'.", fromOs, fromDevice);
        fromOs.Should().Be(fromDevice);
    }

    [SkippableFact]
    public void GetDeviceSerial_succeeds_given_a_device_descriptor_when_device_is_already_open()
    {
        using var openDevice = _deviceSource.OpenUsbDeviceOrSkip();
        _logger.LogInformation(
            "Device open: VID=0x{VID:X4}, PID=0x{PID:X4}, SerialNumber={SerialNumber}.",
            openDevice.Descriptor.VendorId,
            openDevice.Descriptor.ProductId,
            openDevice.GetSerialNumber()
        );
        // Get serial using the descriptor (not the open device)
        var serial = _usb.GetDeviceSerial(openDevice.Descriptor);
        serial.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public void Open_devices_are_auto_disposed_when_the_Usb_type_is_disposed()
    {
        // Open device and leave it open
        var device = _deviceSource.OpenUsbDeviceOrSkip();
        // Dispose Usb to trigger auto disposal of devices
        _usb.Dispose();
        // Attempt to get serial, the device should be auto disposed at this point
        var getSerialAct = () => device.GetSerialNumber();
        getSerialAct.Should().Throw<ObjectDisposedException>();
        var disposeAct = () => device.Dispose();
#if DEBUG
        // Calling dispose in debug throws exception
        disposeAct.Should().Throw<ObjectDisposedException>();
#else
        // Calling dispose again in release only logs warning
        disposeAct.Should().NotThrow();
#endif
    }

    [SkippableFact]
    public void DeviceArrived_is_raised_with_a_descriptor_for_each_connected_device()
    {
        // Snapshot the currently connected devices; enumeration replays these as DeviceArrived.
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(
            expectedKeys.Count == 0,
            "No USB device available to emit a hotplug arrived event."
        );

        var provider = _usb.HotplugProvider;
        var arrived = new ConcurrentQueue<IUsbDeviceDescriptor>();
        using var reachedExpected = new ManualResetEventSlim(false);
        var listener = new TestHotplugListener
        {
            DeviceArrived = descriptor =>
            {
                arrived.Enqueue(descriptor);
                // Events arrive on the libusb event loop thread; signal once we've seen them all.
                if (arrived.Select(k => k.DeviceKey).Distinct().Count() >= expectedKeys.Count)
                    reachedExpected.Set();
            },
        };

        // Registration with LIBUSB_HOTPLUG_ENUMERATE replays the connected devices as
        // DeviceArrived events, delivered on the libusb event loop thread.
        Skip.IfNot(
            provider.RegisterHotplug(listener) == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );
        reachedExpected.Wait(EventTimeout);

        arrived.Should().NotBeEmpty(because: "enumeration should replay connected devices");
        arrived
            .Should()
            .OnlyContain(
                d => d != null && !string.IsNullOrWhiteSpace(d.DeviceKey),
                because: "every emitted event should carry a populated device descriptor"
            );
        arrived
            .Select(d => d.DeviceKey)
            .Should()
            .Contain(
                expectedKeys,
                because: "emitted descriptors should correspond to real connected devices"
            );

        foreach (var descriptor in arrived)
        {
            _logger.LogInformation(
                "DeviceArrived: Class={DeviceClass}, VID=0x{VID:X4}, PID=0x{PID:X4}, Key={Key}.",
                descriptor.DeviceClass,
                descriptor.VendorId,
                descriptor.ProductId,
                descriptor.DeviceKey
            );
        }
    }

    [SkippableFact]
    public void DeviceArrived_descriptor_matches_the_enumerated_device_descriptor()
    {
        var expected = _usb.GetDeviceList().FirstOrDefault();
        Skip.If(expected is null, "No USB device available to emit a hotplug arrived event.");

        var provider = _usb.HotplugProvider;
        var arrived = new ConcurrentDictionary<string, IUsbDeviceDescriptor>();
        using var matched = new ManualResetEventSlim(false);
        var listener = new TestHotplugListener
        {
            DeviceArrived = descriptor =>
            {
                arrived[descriptor.DeviceKey] = descriptor;
                if (arrived.ContainsKey(expected!.DeviceKey))
                    matched.Set();
            },
        };

        Skip.IfNot(
            provider.RegisterHotplug(listener) == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );
        matched.Wait(EventTimeout);

        arrived
            .Should()
            .ContainKey(
                expected!.DeviceKey,
                because: "the enumerated device should be replayed as a DeviceArrived event"
            );
        var emitted = arrived[expected!.DeviceKey];
        emitted.VendorId.Should().Be(expected.VendorId);
        emitted.ProductId.Should().Be(expected.ProductId);
        emitted.BusNumber.Should().Be(expected.BusNumber);
        emitted.BusAddress.Should().Be(expected.BusAddress);
        emitted.DeviceClass.Should().Be(expected.DeviceClass);
    }

    [Fact]
    public void RegisterHotplug_with_a_noop_listener_does_not_throw()
    {
        // With enumeration enabled the callback runs for connected devices; delivering them to a
        // listener whose callbacks do nothing must be safe.
        var provider = _usb.HotplugProvider;
        var act = () => provider.RegisterHotplug(new TestHotplugListener());
        act.Should().NotThrow();
    }

    [SkippableFact]
    public void A_throwing_DeviceArrived_callback_does_not_escape_onto_the_event_loop_thread()
    {
        Skip.If(
            _usb.GetDeviceList().Count == 0,
            "No USB device available to emit a hotplug arrived event."
        );

        var provider = _usb.HotplugProvider;
        using var callbackInvoked = new ManualResetEventSlim(false);
        var listener = new TestHotplugListener
        {
            DeviceArrived = _ =>
            {
                callbackInvoked.Set();
                throw new InvalidOperationException("Callback failure.");
            },
        };

        Skip.IfNot(
            provider.RegisterHotplug(listener) == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );

        callbackInvoked.Wait(EventTimeout).Should().BeTrue();
        // The exception is caught and logged on the event loop thread; the instance stays usable.
        var act = () => _usb.GetDeviceList();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
