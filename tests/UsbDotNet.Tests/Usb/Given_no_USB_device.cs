using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Tests.Usb;

public sealed class Given_no_USB_device : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public Given_no_USB_device(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_no_USB_device>();
    }

    private UsbDotNet.Usb CreateUsb(LogLevel nativeLogLevel = LogLevel.Information) =>
        new(
            _libusb,
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = nativeLogLevel }
        );

    [Fact]
    public void GetVersion_returns_a_valid_version_of_at_least_1_0_27()
    {
        var version = UsbDotNet.Usb.GetVersion();
        _logger.LogInformation("LibUsb version: {Version}", version);
        // Log callback requires v1.0.27 or above
        version.Should().BeGreaterThanOrEqualTo(new Version(1, 0, 27));
    }

    [Fact]
    public void Creating_two_active_instances_of_the_Usb_type_is_not_allowed()
    {
        using var usb1 = CreateUsb();
        var act = () => CreateUsb();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Only one instance of the Usb type allowed.");
    }

    [Fact]
    public void Creating_a_second_instance_of_the_Usb_type_is_allowed_after_disposal_of_first()
    {
        var usb1 = CreateUsb();
        usb1.Dispose();
        using var usb2 = CreateUsb();
    }

    [Fact]
    public void Initialize_throws_when_called_a_second_time()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var act = () => usb.Initialize();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Usb type already initialized.");
    }

    [Fact]
    public void GetDeviceList_throws_when_called_without_Initialize()
    {
        using var usb = CreateUsb();
        var act = () => usb.GetDeviceList();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDeviceList_throws_when_called_after_Dispose()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        usb.Dispose();
        var act = () => usb.GetDeviceList();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RegisterHotplug_throws_when_called_without_Initialize()
    {
        using var usb = CreateUsb();
        var provider = (IHotplugProvider)usb;
        var act = () => provider.RegisterHotplug();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterHotplug_returns_Success_when_called_after_Initialize()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = (IHotplugProvider)usb;
        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.Success);
    }

    [Fact]
    public void Attaching_a_second_DeviceArrived_callback_throws()
    {
        using var usb = CreateUsb();
        var provider = (IHotplugProvider)usb;
        provider.DeviceArrived = _ => { };

        // The callback slots are single-owner: a different non-null callback must be rejected so
        // a second consumer cannot silently steal events from the first.
        var act = () => provider.DeviceArrived = _ => { };
        act.Should().Throw<InvalidOperationException>().WithMessage("*already attached*");

        // Detaching (null) and re-attaching is allowed.
        provider.DeviceArrived = null;
        var reattach = () => provider.DeviceArrived = _ => { };
        reattach.Should().NotThrow();
    }

    [SkippableFact]
    public void RegisterHotplug_returns_AlreadyRegistered_when_called_a_second_time()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = (IHotplugProvider)usb;
        Skip.IfNot(
            provider.RegisterHotplug() == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );

        provider.RegisterHotplug().Should().Be(HotplugRegistrationResult.AlreadyRegistered);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        _loggerFactory.Dispose();
    }
}
