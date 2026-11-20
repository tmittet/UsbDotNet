using FakeItEasy;
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
    public void AddUsbHotplug_monitor_uses_the_hotplug_provider_of_any_registered_IUsb()
    {
        // An IUsb that is not the Usb type; the monitor needs nothing but its public API
        var hotplug = A.Fake<IUsbHotplugProvider>();
        A.CallTo(() => hotplug.IsHotplugSupported).Returns(true);
        var usb = A.Fake<IUsb>();
        A.CallTo(() => usb.Hotplug).Returns(hotplug);
        var services = new ServiceCollection();
        _ = services.AddSingleton(usb).AddUsbHotplug();

        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IUsbHotplugMonitor>();

        monitor.Should().BeOfType<UsbHotplugMonitor>().Which.IsHotplugSupported.Should().BeTrue();
    }

    [Fact]
    public void Create_uses_the_hotplug_provider_of_any_IUsb()
    {
        var hotplug = A.Fake<IUsbHotplugProvider>();
        A.CallTo(() => hotplug.IsHotplugSupported).Returns(true);
        var usb = A.Fake<IUsb>();
        A.CallTo(() => usb.Hotplug).Returns(hotplug);

        using var monitor = UsbHotplugMonitor.Create(usb);

        monitor.IsHotplugSupported.Should().BeTrue();
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
