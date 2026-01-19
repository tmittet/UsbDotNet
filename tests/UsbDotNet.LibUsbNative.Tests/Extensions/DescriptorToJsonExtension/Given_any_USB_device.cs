using System.Text.Json;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.Tests.Extensions.DescriptorToJsonExtension;

public class Given_any_USB_device_Fake(ITestOutputHelper output)
    : Given_any_USB_device(output, new FakeLibusbApi());

[Trait("Category", "UsbDevice")]
public class Given_any_USB_device_Real(ITestOutputHelper output)
    : Given_any_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_any_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void UsbConfigDescriptor_serializes_and_deserializes_successfully()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            var json = device.GetActiveConfigDescriptor().ToJson();
            Output.WriteLine(json);

            var deserialized = JsonSerializer.Deserialize<libusb_config_descriptor>(json)!;
            deserialized.ToJson().Should().Be(json);
        });
    }

    [SkippableFact]
    public void UsbDeviceDescriptor_serializes_and_deserializes_successfully()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var list = context.GetDeviceList();
            var device = list.GetAnyDeviceOrSkipTest();

            var json = device.GetDeviceDescriptor().ToJson();
            Output.WriteLine(json);

            var deserialized = JsonSerializer.Deserialize<libusb_device_descriptor>(json)!;
            deserialized.ToJson().Should().Be(json);
        });
    }
};
