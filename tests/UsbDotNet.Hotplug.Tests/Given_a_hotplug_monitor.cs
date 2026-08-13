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
            _monitor = new UsbHotplugMonitor(_usb.HotplugProvider, _loggerFactory);
        }
        catch
        {
            _usb.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads until at least <paramref name="minCount"/> events are seen, or the enumerator's own
    /// token (the caller's timeout) elapses. Leaves the enumerator open, so the subscription stays
    /// live for tests that need a second subscriber to join while this one is still reading.
    /// </summary>
    private static async Task<List<UsbHotplugEvent>> ReadAtLeastAsync(
        IAsyncEnumerator<UsbHotplugEvent> events,
        int minCount
    )
    {
        var collected = new List<UsbHotplugEvent>();
        try
        {
            while (collected.Count < minCount && await events.MoveNextAsync())
            {
                collected.Add(events.Current);
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out, or the monitor was torn down; return whatever was collected.
        }
        return collected;
    }

    /// <summary>
    /// Reads until at least <paramref name="minCount"/> events are seen or the timeout elapses,
    /// then ends the subscription. Use the enumerator overload when the subscription has to
    /// outlive the read.
    /// </summary>
    private static async Task<List<UsbHotplugEvent>> ReadAtLeastAsync(
        IAsyncEnumerable<UsbHotplugEvent> stream,
        int minCount,
        TimeSpan timeout
    )
    {
        using var cts = new CancellationTokenSource(timeout);
        await using var events = stream.GetAsyncEnumerator(cts.Token);
        return await ReadAtLeastAsync(events, minCount);
    }

    [SkippableFact]
    public async Task Subscribe_emits_Connected_for_each_connected_device()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(expectedKeys.Count == 0, "No USB device available.");

        var events = await ReadAtLeastAsync(_monitor.Subscribe(), expectedKeys.Count, EventTimeout);

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
        using var cts = new CancellationTokenSource(EventTimeout);

        // First subscriber triggers native registration and libusb enumeration, populating the
        // monitor's tracked device set. Its enumerator is held open deliberately: reading through
        // the stream overload would leave the loop and unsubscribe, so this would silently stop
        // covering a late subscriber joining while another subscription is still live.
        await using var first = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        _ = await ReadAtLeastAsync(first, expectedKeys.Count);

        // A late subscriber must receive the current devices from the internal tracked set.
        var snapshot = await ReadAtLeastAsync(
            _monitor.Subscribe(),
            expectedKeys.Count,
            EventTimeout
        );

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

        var events = await ReadAtLeastAsync(
            _monitor.Subscribe(new UsbDeviceFilter(VendorIds: [vendorId])),
            expectedMatchCount,
            EventTimeout
        );

        events.Should().HaveCountGreaterThanOrEqualTo(1);
        events
            .Should()
            .OnlyContain(
                e => e.Descriptor.VendorId == vendorId,
                because: "the filter should exclude non-matching vendors"
            );
    }

    [SkippableFact]
    public async Task Disposing_the_monitor_cancels_all_consumers()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        // A device is needed to prime the two streams deterministically: Subscribe is an iterator
        // method, so a subscription only exists once its first read has run the body, and with no
        // devices that first read parks instead of completing. The parked case is covered
        // hardware-free in the fake-provider suite.
        Skip.If(expectedKeys.Count == 0, "No USB device available.");
        using var cts = new CancellationTokenSource(EventTimeout);
        await using var first = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        await using var second = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        (await first.MoveNextAsync()).Should().BeTrue();
        (await second.MoveNextAsync()).Should().BeTrue();

        _monitor.Dispose();

        // The rest of the replay is still buffered, so this exercises the refuse-to-yield-a-stale
        // event path: teardown surfaces as an OperationCanceledException rather than the loop
        // simply ending, and the exception carries no token, which distinguishes it from this
        // test's own timeout.
        var readFirst = async () => await first.MoveNextAsync();
        var readSecond = async () => await second.MoveNextAsync();
        (await readFirst.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(CancellationToken.None);
        (await readSecond.Should().ThrowAsync<OperationCanceledException>())
            .Which.CancellationToken.Should()
            .Be(CancellationToken.None);
    }

    [Fact]
    public async Task Subscribing_after_dispose_throws_ObjectDisposedException()
    {
        using var cts = new CancellationTokenSource(EventTimeout);
        _monitor.Dispose();

        // The throw lands on the first read, not on the Subscribe call: Subscribe is an iterator
        // method and runs no part of its body until then.
        await using var events = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var read = async () => await events.MoveNextAsync();
        await read.Should().ThrowAsync<ObjectDisposedException>();
    }

    [SkippableFact]
    public async Task A_second_monitor_over_the_same_usb_throws_on_subscribe()
    {
        // Checked before subscribing: skipping with a read in flight would leave the enumerator
        // disposed mid-read, which is undefined.
        Skip.If(
            !_monitor.IsHotplugSupported,
            "Hotplug not supported on this platform; nothing gets registered."
        );
        using var cts = new CancellationTokenSource(EventTimeout);
        // The first monitor registers hotplug on the first read of its first subscription, so the
        // stream has to be started for there to be a registration the second monitor collides with.
        await using var first = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var parked = first.MoveNextAsync();

        using var secondMonitor = new UsbHotplugMonitor(_usb.HotplugProvider, _loggerFactory);
        await using var second = secondMonitor.Subscribe().GetAsyncEnumerator(cts.Token);
        var read = async () => await second.MoveNextAsync();
        (await read.Should().ThrowAsync<InvalidOperationException>()).WithMessage(
            "*only one UsbHotplugMonitor may be active per IUsb*"
        );

        // Release the first stream before its enumerator is disposed. With devices attached its
        // read has already completed with a replayed event; with none it is still parked and
        // Dispose is what wakes it.
        _monitor.Dispose();
        try
        {
            _ = await parked;
        }
        catch (OperationCanceledException)
        {
            // Expected when the read was still parked.
        }
    }

    [SkippableFact]
    public async Task A_new_monitor_can_subscribe_after_the_previous_monitor_is_disposed()
    {
        var expectedKeys = _usb.GetDeviceList().Select(d => d.DeviceKey).ToHashSet();
        Skip.If(expectedKeys.Count == 0, "No USB device available.");
        using var cts = new CancellationTokenSource(EventTimeout);
        await using var first = _monitor.Subscribe().GetAsyncEnumerator(cts.Token);
        Skip.If(
            !_monitor.IsHotplugSupported,
            "Hotplug not supported on this platform; nothing gets registered."
        );
        _ = await ReadAtLeastAsync(first, expectedKeys.Count);

        // Disposing the monitor releases its hotplug registration on the IUsb.
        _monitor.Dispose();

        using var secondMonitor = new UsbHotplugMonitor(_usb.HotplugProvider, _loggerFactory);

        // The new registration re-enumerates with a cleared device cache, so the connected
        // devices are replayed rather than suppressed as duplicate arrivals.
        var events = await ReadAtLeastAsync(
            secondMonitor.Subscribe(),
            expectedKeys.Count,
            EventTimeout
        );
        events.Should().OnlyContain(e => e.Type == UsbHotplugEventType.Connected);
        events
            .Select(e => e.Descriptor.DeviceKey)
            .Should()
            .Contain(
                expectedKeys,
                because: "a new monitor must receive a fresh enumeration of connected devices"
            );
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _usb.Dispose();
        _loggerFactory.Dispose();
    }
}
