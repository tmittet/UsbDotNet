using Microsoft.Extensions.Logging;

namespace UsbDotNet;

/// <summary>
/// Configuration options for UsbDotNet.
/// </summary>
public sealed class UsbDotNetOptions
{
    /// <summary>
    /// The log level forwarded to the native libusb library via <c>LIBUSB_OPTION_LOG_LEVEL</c>.
    /// Set to <see cref="LogLevel.None"/> to skip registering the libusb log callback.
    /// Defaults to <see cref="LogLevel.Warning"/>.
    /// </summary>
    public LogLevel NativeLibraryLogLevel { get; set; } = LogLevel.Warning;
}
