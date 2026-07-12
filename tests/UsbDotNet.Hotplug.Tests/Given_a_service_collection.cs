using Microsoft.Extensions.DependencyInjection;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_service_collection
{
    [Fact]
    public void AddUsbHotplug_registers_IUsbHotplugMonitor_as_a_singleton()
    {
        var services = new ServiceCollection();
        _ = services.AddUsbDotNet().AddUsbHotplug();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IUsbHotplugMonitor>();
        var second = provider.GetRequiredService<IUsbHotplugMonitor>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddUsbHotplug_monitor_resolves_the_registered_IUsb()
    {
        var services = new ServiceCollection();
        _ = services.AddUsbDotNet().AddUsbHotplug();

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IUsbHotplugMonitor>();

        act.Should().NotThrow();
    }
}
