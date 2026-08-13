using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Hotplug.Tests;

public sealed class Given_an_uninitialized_usb : IDisposable
{
    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly UsbDotNet.Usb _usb;

    public Given_an_uninitialized_usb(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        // NOTE: Initialize() is intentionally not called.
        _usb = new UsbDotNet.Usb(
            _libusb,
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.Warning }
        );
    }

    [SkippableFact]
    public async Task Subscribe_throws_when_usb_is_not_initialized()
    {
        using var monitor = new UsbHotplugMonitor(_usb.HotplugProvider, _loggerFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        Exception? caught = null;
        try
        {
            // Registration happens on the first read, not on the Subscribe call, so the
            // initialization check lands there.
            await using var events = monitor.Subscribe().GetAsyncEnumerator(cts.Token);
            _ = await events.MoveNextAsync();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        // On platforms without hotplug support the monitor never registers (and never reaches the
        // initialization check), so there is nothing to assert.
        Skip.If(
            !monitor.IsHotplugSupported,
            "Hotplug not supported on this platform; registration is skipped before init check."
        );
        caught
            .Should()
            .NotBeNull(because: "the monitor must require an initialized IUsb before subscribing");
    }

    public void Dispose()
    {
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
