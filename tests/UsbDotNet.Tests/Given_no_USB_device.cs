using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Tests;

public sealed class Given_no_USB_device : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;

    public Given_no_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
    }

    [Fact]
    public void GetVersion_returns_a_valid_version_of_at_least_1_0_27()
    {
        var version = Usb.GetVersion();
        // Log callback requires v1.0.27 or above
        version.Should().BeGreaterThanOrEqualTo(new Version(1, 0, 27));
    }

    [Fact]
    public void Creating_two_active_instances_of_the_Usb_type_is_not_allowed()
    {
        using var usb1 = new Usb(_libusb, _loggerFactory);
        var act = () => new Usb(_libusb, _loggerFactory);
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Only one instance of the Usb type allowed.");
    }

    [Fact]
    public void Creating_a_second_instance_of_the_Usb_type_is_allowed_after_disposal_of_first()
    {
        var usb1 = new Usb(_libusb, _loggerFactory);
        usb1.Dispose();
        using var usb2 = new Usb(_libusb, _loggerFactory);
    }

    [Fact]
    public void Initialize_throws_when_called_a_second_time()
    {
        using var usb = new Usb(_libusb, _loggerFactory);
        usb.Initialize();
        var act = () => usb.Initialize();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Usb type already initialized.");
    }

    [Fact]
    public void GetDeviceList_throws_when_called_without_Initialize()
    {
        using var usb = new Usb(_libusb, _loggerFactory);
        var act = () => usb.GetDeviceList();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDeviceList_throws_when_called_after_Dispose()
    {
        using var usb = new Usb(_libusb, _loggerFactory);
        usb.Initialize(LogLevel.Information);
        usb.Dispose();
        var act = () => usb.GetDeviceList();
        act.Should().Throw<ObjectDisposedException>();
    }

    [SkippableFact]
    public void RegisterHotplug_throws_when_called_without_Initialize_on_supported_platform()
    {
        Skip.If(
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS(),
            "Hotplug only supported on Linux and macOS."
        );

        using var usb = new Usb(_libusb, _loggerFactory);
        var act = () => usb.RegisterHotplug(vendorId: 0x2BD9);
        act.Should().Throw<InvalidOperationException>();
    }

    [SkippableFact]
    public void RegisterHotplug_returns_true_when_called_after_Initialize_on_supported_platform()
    {
        Skip.If(
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS(),
            "Hotplug only supported on Linux and macOS."
        );

        using var usb = new Usb(_libusb, _loggerFactory);
        usb.Initialize(LogLevel.Information);
        var success = usb.RegisterHotplug(vendorId: 0x2BD9);
        success.Should().BeTrue();
    }

    [Fact]
    public void RegisterHotplug_returns_false_when_called_after_Initialize_on_unsupported_platform()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var usb = new Usb(_libusb, _loggerFactory);
        usb.Initialize(LogLevel.Information);
        var success = usb.RegisterHotplug(vendorId: 0x2BD9);
        success.Should().BeFalse();
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }
}
