using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UsbDotNet.Tests.UsbDotNetServiceCollectionExtensions;

public class Given_no_USB_device
{
    [Fact]
    public void AddUsbDotNet_allows_IUsb_to_be_resolved_without_AddLogging()
    {
        var services = new ServiceCollection();
        _ = services.AddUsbDotNet();

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IUsb>();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddUsbDotNet_registers_IUsb_as_a_singleton()
    {
        var services = new ServiceCollection();
        _ = services.AddUsbDotNet();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IUsb>();
        var second = provider.GetRequiredService<IUsb>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddUsbDotNet_applies_configure_callback_to_UsbDotNetOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddUsbDotNet(o => o.NativeLibraryLogLevel = LogLevel.Debug);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<UsbDotNetOptions>>().Value;

        options.NativeLibraryLogLevel.Should().Be(LogLevel.Debug);
    }
}
