using System.Collections.Concurrent;
using UsbDotNet.Descriptor;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Hotplug.Tests;

[Trait("Category", "UsbDevice")]
public sealed class Given_a_hotplug_event_notifier : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Usb _usb;
    private readonly UsbHotplugMonitor _monitor;

    public Given_a_hotplug_event_notifier(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _usb = new Usb(
            _libusb,
            _loggerFactory,
            new UsbDotNetOptions { NativeLibraryLogLevel = LogLevel.Warning }
        );
        try
        {
            _usb.Initialize();
            _monitor = new UsbHotplugMonitor((IHotplugProvider)_usb, _loggerFactory);
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    [SkippableFact]
    public void DeviceConnected_event_is_raised_for_connected_devices()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(expectedKeys.Count == 0, "No USB device available.");

        var connected = new ConcurrentQueue<IUsbDeviceDescriptor>();
        using var reachedExpected = new ManualResetEventSlim(false);
        using var notifier = new UsbHotplugEventNotifier(_monitor, filter: null, _loggerFactory);
        notifier.DeviceConnected += (_, e) =>
        {
            connected.Enqueue(e.Descriptor);
            if (connected.Count >= expectedKeys.Count)
                reachedExpected.Set();
        };
        // Attach handlers first, then start pumping so the initial snapshot is delivered.
        notifier.Start();

        reachedExpected.Wait(EventTimeout);

        connected.Should().NotBeEmpty();
        connected.Select(d => d.DeviceKey).Should().Contain(key => expectedKeys.Contains(key));
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
