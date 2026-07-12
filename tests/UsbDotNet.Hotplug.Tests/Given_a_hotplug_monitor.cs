using System.Threading.Channels;
using UsbDotNet.Internal;
using UsbDotNet.LibUsbNative;

namespace UsbDotNet.Hotplug.Tests;

[Trait("Category", "UsbDevice")]
public sealed class Given_a_hotplug_monitor : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly ILibUsb _libusb;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Given_a_hotplug_monitor> _logger;
    private readonly UsbDotNet.Usb _usb;
    private readonly UsbHotplugMonitor _monitor;

    public Given_a_hotplug_monitor(ITestOutputHelper output)
    {
        _libusb = new LibUsb();
        _loggerFactory = new TestLoggerFactory(output);
        _logger = _loggerFactory.CreateLogger<Given_a_hotplug_monitor>();
        _usb = new UsbDotNet.Usb(
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

    /// <summary>Reads until at least <paramref name="minCount"/> events are seen or the timeout elapses.</summary>
    private static async Task<List<UsbHotplugEvent>> ReadAtLeastAsync(
        ChannelReader<UsbHotplugEvent> reader,
        int minCount,
        TimeSpan timeout
    )
    {
        var events = new List<UsbHotplugEvent>();
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await foreach (var e in reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
            {
                events.Add(e);
                if (events.Count >= minCount)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out; return whatever was collected.
        }
        return events;
    }

    [SkippableFact]
    public async Task Subscribe_emits_Connected_for_each_connected_device()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(expectedKeys.Count == 0, "No USB device available.");

        using var subscription = _monitor.Subscribe();
        var events = await ReadAtLeastAsync(subscription.Reader, expectedKeys.Count, EventTimeout);

        events.Should().NotBeEmpty(because: "enumeration should replay connected devices");
        events
            .Should()
            .OnlyContain(
                e => e.Type == UsbHotplugEventType.Connected,
                because: "enumeration replays connected devices as Connected"
            );
        events
            .Select(e => e.Descriptor.DeviceKey)
            .Should()
            .Contain(
                expectedKeys,
                because: "emitted descriptors should correspond to real connected devices"
            );
    }

    [SkippableFact]
    public async Task A_late_subscriber_receives_a_snapshot_of_connected_devices()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(expectedKeys.Count == 0, "No USB device available.");

        // First subscriber triggers native registration and libusb enumeration, populating the
        // monitor's tracked device set.
        using var first = _monitor.Subscribe();
        _ = await ReadAtLeastAsync(first.Reader, expectedKeys.Count, EventTimeout);

        // A late subscriber must receive the current devices from the internal tracked set.
        using var late = _monitor.Subscribe();
        var snapshot = await ReadAtLeastAsync(late.Reader, expectedKeys.Count, EventTimeout);

        snapshot.Should().OnlyContain(e => e.Type == UsbHotplugEventType.Connected);
        snapshot
            .Select(e => e.Descriptor.DeviceKey)
            .Should()
            .Contain(
                expectedKeys,
                because: "a late subscriber should be caught up with all connected devices"
            );
    }

    [SkippableFact]
    public async Task A_filtered_subscriber_only_receives_matching_devices()
    {
        var devices = _usb.GetDeviceList();
        Skip.If(devices.Count == 0, "No USB device available.");

        var vendorId = devices.First().VendorId;
        var expectedMatchCount = devices.Count(d => d.VendorId == vendorId);

        using var subscription = _monitor.Subscribe(new UsbDeviceFilter(VendorId: vendorId));
        var events = await ReadAtLeastAsync(subscription.Reader, expectedMatchCount, EventTimeout);

        events.Should().HaveCountGreaterThanOrEqualTo(1);
        events
            .Should()
            .OnlyContain(
                e => e.Descriptor.VendorId == vendorId,
                because: "the filter should exclude non-matching vendors"
            );
    }

    /// <summary>Drains the reader to completion, or throws if it does not complete within the timeout.</summary>
    private static async Task DrainToCompletionAsync(
        ChannelReader<UsbHotplugEvent> reader,
        TimeSpan timeout
    )
    {
        using var cts = new CancellationTokenSource(timeout);
        // Once the writer is completed, ReadAllAsync yields any buffered events then ends. If the
        // channel is never completed this cancels and throws, failing the test.
        await foreach (var _ in reader.ReadAllAsync(cts.Token).ConfigureAwait(false)) { }
    }

    [Fact]
    public async Task Disposing_a_subscription_completes_its_reader()
    {
        var subscription = _monitor.Subscribe();
        subscription.Dispose();

        // A completed channel drains (possibly buffered enumeration events) then ends the loop.
        await DrainToCompletionAsync(subscription.Reader, EventTimeout);
        subscription.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Disposing_the_monitor_completes_all_subscription_readers()
    {
        var first = _monitor.Subscribe();
        var second = _monitor.Subscribe();

        _monitor.Dispose();

        await DrainToCompletionAsync(first.Reader, EventTimeout);
        await DrainToCompletionAsync(second.Reader, EventTimeout);
        first.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
        second.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Subscribe_after_dispose_throws_ObjectDisposedException()
    {
        _monitor.Dispose();
        var act = () => _monitor.Subscribe();
        act.Should().Throw<ObjectDisposedException>();
    }

    [SkippableFact]
    public void A_second_monitor_over_the_same_usb_throws_on_subscribe()
    {
        // The first monitor registers hotplug on its first subscription.
        using var first = _monitor.Subscribe();
        Skip.If(
            !_monitor.IsHotplugSupported,
            "Hotplug not supported on this platform; nothing gets registered."
        );

        using var secondMonitor = new UsbHotplugMonitor((IHotplugProvider)_usb, _loggerFactory);
        var act = () => secondMonitor.Subscribe();
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*only one UsbHotplugMonitor may be active per IUsb*");
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
