using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_a_service_collection_with_a_custom_usb
{

    [Fact]
    public void AddUsbHotplug_before_custom_IUsb()
    {
        var services = new ServiceCollection()
            .AddUsbHotplug()
            .AddSingleton<IUsb>(_ => CreateFakeUsb());

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IUsbHotplugMonitor>();
    }

    [Fact]
    public void AddUsbHotplug_after_custom_IUsb()
    {
        // No AddUsbDotNet: nothing registers IHotplugProvider, which is what the monitor is built
        // over. Registration itself succeeds; the failure surfaces on first resolve.
        var services = new ServiceCollection()
            .AddSingleton<IUsb>(_ => CreateFakeUsb())
            .AddUsbHotplug();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IUsbHotplugMonitor>());
    }

    private static IUsb CreateFakeUsb()
    {
        var provider = A.Fake<IUsb>();
        A.CallTo(() => provider.IsHotplugSupported).Returns(true);
        return provider;
    }


}
