using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;
using UsbDotNet.Tests.Fakes;

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
        var provider = usb.HotplugProvider;
        var act = () => provider.RegisterHotplug(new TestHotplugListener());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterHotplug_returns_Success_when_called_after_Initialize()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = usb.HotplugProvider;
        provider
            .RegisterHotplug(new TestHotplugListener())
            .Should()
            .Be(HotplugRegistrationResult.Success);
    }

    [SkippableFact]
    public void RegisterHotplug_returns_AlreadyRegistered_when_called_a_second_time()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = usb.HotplugProvider;
        Skip.IfNot(
            provider.RegisterHotplug(new TestHotplugListener())
                == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );

        // The second caller's listener is not attached; the first registration keeps its owner.
        provider
            .RegisterHotplug(new TestHotplugListener())
            .Should()
            .Be(HotplugRegistrationResult.AlreadyRegistered);
    }

    [SkippableFact]
    public void RegisterHotplug_succeeds_again_after_the_owner_deregisters()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = usb.HotplugProvider;
        var owner = new TestHotplugListener();
        Skip.IfNot(
            provider.RegisterHotplug(owner) == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );

        provider.DeregisterHotplug(owner);

        provider
            .RegisterHotplug(new TestHotplugListener())
            .Should()
            .Be(HotplugRegistrationResult.Success);
    }

    [SkippableFact]
    public void DeregisterHotplug_by_a_non_owner_does_not_release_the_registration()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var provider = usb.HotplugProvider;
        Skip.IfNot(
            provider.RegisterHotplug(new TestHotplugListener())
                == HotplugRegistrationResult.Success,
            "Hotplug is not supported on this platform."
        );

        provider.DeregisterHotplug(new TestHotplugListener());

        provider
            .RegisterHotplug(new TestHotplugListener())
            .Should()
            .Be(HotplugRegistrationResult.AlreadyRegistered);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        _loggerFactory.Dispose();
    }
}
