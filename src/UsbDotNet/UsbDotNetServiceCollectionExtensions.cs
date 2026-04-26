using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UsbDotNet;
using UsbDotNet.LibUsbNative;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods to register UsbDotNet services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class UsbDotNetServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IUsb"/> and a default <see cref="ILibUsb"/> as singletons.
    /// Loggers for <see cref="Usb"/> and its sub-components are resolved from the
    /// <see cref="ILoggerFactory"/> registered in the service collection.
    /// </summary>
    public static IServiceCollection AddUsbDotNet(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ILibUsb, LibUsb>();
        services.TryAddSingleton<IUsb>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var libUsb = sp.GetRequiredService<ILibUsb>();
            return new Usb(libUsb, loggerFactory, loggerFactory.CreateLogger<Usb>());
        });
        return services;
    }
}
