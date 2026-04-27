using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    /// <para>NOTE: Call IUsb.Initialize() before enumerating or opening devices.</para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional configurator for <see cref="UsbDotNetOptions"/>. Equivalent to calling
    /// <c>services.Configure&lt;UsbDotNetOptions&gt;(configure)</c>.
    /// </param>
    public static IServiceCollection AddUsbDotNet(
        this IServiceCollection services,
        Action<UsbDotNetOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ensure ILoggerFactory is resolvable on a bare ServiceCollection; no-op if already added.
        _ = services.AddLogging();
        _ = services.AddOptions<UsbDotNetOptions>();
        if (configure is not null)
        {
            _ = services.Configure(configure);
        }
        services.TryAddSingleton<ILibUsb, LibUsb>();
        services.TryAddSingleton<IUsb>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var libUsb = sp.GetRequiredService<ILibUsb>();
            var options = sp.GetRequiredService<IOptions<UsbDotNetOptions>>().Value;
            return new Usb(libUsb, loggerFactory, options);
        });
        return services;
    }
}
