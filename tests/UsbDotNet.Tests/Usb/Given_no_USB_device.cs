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
        var act = () => usb.RegisterHotplug(vendorId: 0x2BD9);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterHotplug_returns_true_when_called_after_Initialize()
    {
        using var usb = CreateUsb();
        usb.Initialize();
        var success = usb.RegisterHotplug(vendorId: 0x2BD9);
        success.Should().BeTrue();
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        _loggerFactory.Dispose();
    }
}
