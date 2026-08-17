using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UsbDotNet;
using UsbDotNet.Hotplug;
using UsbDotNet.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods to register UsbDotNet hotplug services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class UsbHotplugServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IUsbHotplugMonitor"/> as a singleton over the registered
    /// <see cref="IUsb"/>. Requires <c>AddUsbDotNet()</c> and that <see cref="IUsb.Initialize()"/>
    /// is called before the first subscription.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddUsbHotplug(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddLogging();
        services.TryAddSingleton<IUsbHotplugMonitor>(sp => new UsbHotplugMonitor(
            sp.GetRequiredService<IHotplugProvider>(),
            sp.GetService<ILoggerFactory>()
        ));
        return services;
    }
}
