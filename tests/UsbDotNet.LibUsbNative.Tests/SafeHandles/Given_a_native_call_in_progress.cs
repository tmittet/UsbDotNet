using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles;

/// <summary>
/// A safe handle that is disposed while one of its native calls is in progress must not release
/// the native resource until the call completes.
/// </summary>
public class Given_a_native_call_in_progress
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A native call that is held in progress, the disposal that races it, and a count of the
    /// native release that must be deferred until the call completes.
    /// </summary>
    private sealed record Scenario(
        string ApiName,
        Action Call,
        Action Dispose,
        Func<int> ReleaseCount,
        int ExpectedReleaseCount = 1
    );

    private static readonly Dictionary<string, Func<FakeLibusbApi, Scenario>> Scenarios = new()
    {
        // ---- SafeContext, released with libusb_exit
        ["SafeContext.SetOption(int)"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_set_option),
                context => context.SetOption(libusb_option.LIBUSB_OPTION_LOG_LEVEL, 0)
            ),
        ["SafeContext.SetOption(nint)"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_set_option),
                context => context.SetOption(libusb_option.LIBUSB_OPTION_LOG_CB, IntPtr.Zero)
            ),
        ["SafeContext.HandleEventsCompleted"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_handle_events_completed),
                context => context.HandleEventsCompleted(new IntPtr(1))
            ),
        ["SafeContext.InterruptEventHandler"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_interrupt_event_handler),
                context => context.InterruptEventHandler()
            ),
        ["SafeContext.RegisterHotplugCallback"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_hotplug_register_callback),
                // The callback handle keeps the context alive until it is disposed
                context =>
                    context
                        .RegisterHotplugCallback(
                            libusb_hotplug_event.LIBUSB_HOTPLUG_EVENT_DEVICE_ARRIVED,
                            libusb_hotplug_flag.NONE,
                            (_, _, _) => libusb_hotplug_return.REARM
                        )
                        .Dispose()
            ),
        ["SafeContext.GetDeviceList"] = api =>
            ContextScenario(
                api,
                nameof(api.libusb_get_device_list),
                // The device list keeps the context alive until it is disposed
                context => context.GetDeviceList().Dispose()
            ),
        // ---- SafeDevice, released with libusb_unref_device
        ["SafeDevice.GetDeviceDescriptor"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_get_device_descriptor),
                device => device.GetDeviceDescriptor()
            ),
        ["SafeDevice.GetActiveConfigDescriptor"] = api =>
            DeviceScenario(
                api,
                // The fake forwards the active config descriptor to this call
                nameof(api.libusb_get_config_descriptor),
                device => device.GetActiveConfigDescriptor()
            ),
        ["SafeDevice.GetConfigDescriptor"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_get_config_descriptor),
                device => device.GetConfigDescriptor(0)
            ),
        ["SafeDevice.GetBusNumber"] = api =>
            DeviceScenario(api, nameof(api.libusb_get_bus_number), device => device.GetBusNumber()),
        ["SafeDevice.GetDeviceAddress"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_get_device_address),
                device => device.GetDeviceAddress()
            ),
        ["SafeDevice.GetPortNumber"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_get_port_number),
                device => device.GetPortNumber()
            ),
        ["SafeDevice.GetSpeed"] = api =>
            DeviceScenario(api, nameof(api.libusb_get_device_speed), device => device.GetSpeed()),
        ["SafeDevice.TryGetDeviceString"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_get_device_string),
                device =>
                    device.TryGetDeviceString(
                        libusb_device_string_type.LIBUSB_DEVICE_STRING_PRODUCT,
                        out _,
                        out _
                    )
            ),
        ["SafeDevice.Open"] = api =>
            DeviceScenario(
                api,
                nameof(api.libusb_open),
                device => device.Open().Dispose(),
                // The device handle holds, and releases, a device reference of its own
                expectedReleaseCount: 2
            ),
        // ---- SafeDeviceHandle, released with libusb_close
        ["SafeDeviceHandle.TryGetStringDescriptorAscii"] = api =>
            DeviceHandleScenario(
                api,
                nameof(api.libusb_get_string_descriptor_ascii),
                handle => handle.TryGetStringDescriptorAscii(3, out _, out _)
            ),
        ["SafeDeviceHandle.ClaimInterface"] = api =>
            DeviceHandleScenario(
                api,
                nameof(api.libusb_claim_interface),
                handle => handle.ClaimInterface(0).Dispose()
            ),
        ["SafeDeviceHandle.ResetDevice"] = api =>
            DeviceHandleScenario(
                api,
                nameof(api.libusb_reset_device),
                handle => handle.ResetDevice()
            ),
        // ---- SafeTransfer, released with libusb_free_transfer
        ["SafeTransfer.Submit"] = api =>
            TransferScenario(
                api,
                nameof(api.libusb_submit_transfer),
                transfer => transfer.Submit()
            ),
        ["SafeTransfer.Cancel"] = api =>
            TransferScenario(
                api,
                nameof(api.libusb_cancel_transfer),
                transfer => transfer.Cancel()
            ),
        // ---- SafeDeviceInterface, released with libusb_release_interface
        ["SafeDeviceInterface.SetAlternateSetting"] = api =>
        {
            var context = new LibUsb(api).CreateContext();
            var list = context.GetDeviceList();
            var handle = list[0].Open();
            var deviceInterface = handle.ClaimInterface(0);
            return new Scenario(
                nameof(api.libusb_set_interface_alt_setting),
                () => deviceInterface.SetAlternateSetting(1),
                deviceInterface.Dispose,
                () => api.ReleaseInterfaceCount
            );
        },
    };

    public static TheoryData<string> ScenarioNames => [.. Scenarios.Keys];

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task The_native_resource_is_not_released_until_the_call_completes(
        string scenarioName
    )
    {
        var api = new FakeLibusbApi();
        var scenario = Scenarios[scenarioName](api);

        await RunAndVerifyDeferredRelease(api, scenario);
    }

    [Fact]
    public async Task SetAlternateSetting_keeps_the_device_handle_open_until_the_call_completes()
    {
        // The interface makes its native call on the device handle, which it does not own
        var api = new FakeLibusbApi();
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();
        var handle = list[0].Open();
        var deviceInterface = handle.ClaimInterface(0);
        var scenario = new Scenario(
            nameof(api.libusb_set_interface_alt_setting),
            () => deviceInterface.SetAlternateSetting(1),
            () =>
            {
                handle.Dispose();
                list.Dispose();
                context.Dispose();
            },
            () => api.CloseCount
        );

        await RunAndVerifyDeferredRelease(api, scenario);
    }

    /// <summary>
    /// Hold the native call of the scenario in progress, dispose, and verify that the native
    /// release happens only when the call has completed.
    /// </summary>
    private static async Task RunAndVerifyDeferredRelease(FakeLibusbApi api, Scenario scenario)
    {
        using var enteredNativeCall = new ManualResetEventSlim();
        using var releaseNativeCall = new ManualResetEventSlim();
        api.CallHooks[scenario.ApiName] = () =>
        {
            enteredNativeCall.Set();
            releaseNativeCall.Wait();
        };

        var call = Task.Run(scenario.Call);
        try
        {
            enteredNativeCall.Wait(Timeout).Should().BeTrue();

            scenario.Dispose();

            scenario.ReleaseCount().Should().Be(0);
        }
        finally
        {
            releaseNativeCall.Set();
        }

        // The call completes without an exception, and then the native resource is released
        await call;
        scenario.ReleaseCount().Should().Be(scenario.ExpectedReleaseCount);
    }

    private static Scenario ContextScenario(
        FakeLibusbApi api,
        string apiName,
        Action<ISafeContext> call
    )
    {
        var context = new LibUsb(api).CreateContext();
        return new Scenario(apiName, () => call(context), context.Dispose, () => api.ExitCount);
    }

    private static Scenario DeviceScenario(
        FakeLibusbApi api,
        string apiName,
        Action<ISafeDevice> call,
        int expectedReleaseCount = 1
    )
    {
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();
        var device = list[0];
        return new Scenario(
            apiName,
            () => call(device),
            // Disposing the list disposes its devices
            () =>
            {
                list.Dispose();
                context.Dispose();
            },
            () => api.UnrefDevices.Count,
            expectedReleaseCount
        );
    }

    private static Scenario DeviceHandleScenario(
        FakeLibusbApi api,
        string apiName,
        Action<ISafeDeviceHandle> call
    )
    {
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();
        var handle = list[0].Open();
        return new Scenario(
            apiName,
            () => call(handle),
            () =>
            {
                handle.Dispose();
                list.Dispose();
                context.Dispose();
            },
            () => api.CloseCount
        );
    }

    private static Scenario TransferScenario(
        FakeLibusbApi api,
        string apiName,
        Action<ISafeTransfer> call
    )
    {
        var context = new LibUsb(api).CreateContext();
        var list = context.GetDeviceList();
        var handle = list[0].Open();
        var transfer = handle.AllocateTransfer();
        return new Scenario(
            apiName,
            () => call(transfer),
            transfer.Dispose,
            () => api.FreeTransferCount
        );
    }
}
