using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeContext;

public class Given_no_USB_device_Fake(ITestOutputHelper output)
    : Given_no_USB_device(output, new FakeLibusbApi());

public class Given_no_USB_device_Real(ITestOutputHelper output)
    : Given_no_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_no_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [Fact]
    public void Disposing_SafeContext_with_open_SafeDeviceList_blocks_context_ReleaseHandle()
    {
        var context = GetContext();
        var list = context.GetDeviceList();
        context.Dispose();

        // SafeContext handle will not be closed until after SafeDeviceList is disposed
        context.IsClosed.Should().BeFalse();
        _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));

        list.Dispose();
    }

    [Fact]
    public void SafeContext_does_ReleaseHandle_when_open_SafeDeviceList_is_disposed()
    {
        var context = GetContext();
        var list = context.GetDeviceList();
        context.Dispose();
        list.Dispose();

        // SafeContext handle should be closed when SafeDeviceList is disposed
        context.IsClosed.Should().BeTrue();
        _ = LibUsbOutput.Should().NotContain(s => s.Contains("still referenced"));
    }

    [Fact]
    public void GetDeviceList_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        Action act = () => context.GetDeviceList();
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RegisterLogCallback_is_successful_when_called_once()
    {
        using var context = GetContext(Enums.libusb_log_level.LIBUSB_LOG_LEVEL_NONE);
        context.RegisterLogCallback((_, message) => Output.WriteLine(message));

        // Verify context is closed after dispose
        context.Dispose();
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RegisterLogCallback_throws_InvalidOperationException_when_called_more_than_once()
    {
        using var context = GetContext(Enums.libusb_log_level.LIBUSB_LOG_LEVEL_NONE);
        context.RegisterLogCallback((_, message) => { });
        var act = () =>
        {
            context.RegisterLogCallback((level, message) => { });
        };
        act.Should().Throw<InvalidOperationException>();

        // Verify context is closed after dispose
        context.Dispose();
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RegisterLogCallback_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        var act = () =>
        {
            context.RegisterLogCallback((level, message) => { });
        };
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }

    private const libusb_hotplug_event HpEvents =
        libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED
        | libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_LEFT;
    private const libusb_hotplug_flag HpFlags = libusb_hotplug_flag.NONE;

    [SkippableTheory]
    [InlineData(5)]
    public void RegisterHotplugCallback_can_have_many_active_callbacks(int callbackCount)
    {
        Skip.If(
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS(),
            "Hotplug only supported on Linux and macOS."
        );

        using var context = GetContext();
        var callbacks = Enumerable
            .Range(0, callbackCount)
            .Select(_ =>
                context.RegisterHotplugCallback(
                    HpEvents,
                    HpFlags,
                    (c, d, e) => libusb_hotplug_return.REARM
                )
            );

        foreach (var callback in callbacks)
        {
            callback.Dispose();
        }

        // Verify context is closed after dispose
        context.Dispose();
        context.IsClosed.Should().BeTrue();
    }

    [SkippableFact]
    public void Not_disposing_RegisterHotplugCallback_handle_blocks_SafeContext_handle_close()
    {
        Skip.If(
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS(),
            "Hotplug only supported on Linux and macOS."
        );

        using var context = GetContext();
        var cbHandle = context.RegisterHotplugCallback(
            HpEvents,
            HpFlags,
            (c, d, e) => libusb_hotplug_return.REARM
        );
        context.Dispose();

        // SafeContext handle will not be closed until after SafeHotplugCallbackHandle is disposed
        context.IsClosed.Should().BeFalse();
        cbHandle.Dispose();
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RegisterHotplugCallback_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        var act = () =>
            context.RegisterHotplugCallback(0, 0, (c, d, e) => libusb_hotplug_return.REARM);
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void SetOption_int_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        var act = () => context.SetOption(0, 0);
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void SetOption_IntPtr_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        var act = () => context.SetOption(0, IntPtr.Zero);
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void HandleEventsCompleted_throws_ObjectDisposedException_after_SafeContext_Dispose()
    {
        var context = GetContext();
        context.Dispose();
        var act = () => context.HandleEventsCompleted(0);
        act.Should().Throw<ObjectDisposedException>();

        // Verify context is closed after dispose
        context.IsClosed.Should().BeTrue();
    }
};
