namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDeviceHandle;

[Trait("Category", "UsbDevice")]
public class Given_an_accessible_USB_device_Real(ITestOutputHelper output)
    : Given_an_accessible_USB_device(output, new PInvokeLibUsbApi());

public abstract class Given_an_accessible_USB_device(ITestOutputHelper output, ILibUsbApi api)
    : LibUsbNativeTestBase(output, api)
{
    [SkippableFact]
    public void SafeDevice_Open_returns_an_open_SafeDeviceHandle()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            using var deviceHandle = device.Open();
            deviceHandle.IsClosed.Should().BeFalse();

            // Verify context is closed after dispose
            context.Dispose();
            deviceList.Dispose();
            deviceHandle.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void GetStringDescriptorAscii_successfully_returns_a_non_empty_string()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            using var deviceHandle = device.Open();
            deviceHandle.IsClosed.Should().BeFalse();

            var serialNumber = deviceHandle.GetStringDescriptorAscii(
                deviceHandle.Device.GetDeviceDescriptor().iSerialNumber
            );
            serialNumber.Should().NotBeNullOrEmpty();
            Output.WriteLine($"Serial Number: {serialNumber}");

            // Verify context is closed after dispose
            deviceHandle.Dispose();
            deviceList.Dispose();
            context.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void TryGetStringDescriptorAscii_successfully_returns_serial()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            using var deviceHandle = device.Open();

            var snIndex = device.GetDeviceDescriptor().iSerialNumber;
            var result = deviceHandle.TryGetStringDescriptorAscii(
                snIndex,
                out var value,
                out var error
            );
            result.Should().BeTrue();
            value.Should().NotBeNullOrEmpty();
            error.Should().BeNull();
            Output.WriteLine($"Serial Number: {value}");

            // Verify context is closed after dispose
            deviceHandle.Dispose();
            deviceList.Dispose();
            context.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }

    [SkippableFact]
    public void Methods_throw_ObjectDisposedException_after_SafeDeviceHandle_Dispose()
    {
        EnterReadLock(() =>
        {
            using var context = GetContext();
            using var deviceList = context.GetDeviceList();
            var device = deviceList.GetAccessibleDeviceOrSkipTest();

            var deviceHandle = device.Open();
            deviceHandle.Dispose();

            Action act = () => deviceHandle.ClaimInterface(1);
            act.Should().Throw<ObjectDisposedException>();

            act = () =>
            {
                var d = deviceHandle.Device;
            };
            act.Should().Throw<ObjectDisposedException>();

            act = () => deviceHandle.GetStringDescriptorAscii(1);
            act.Should().Throw<ObjectDisposedException>();

            act = () => deviceHandle.ResetDevice();
            act.Should().Throw<ObjectDisposedException>();

            // Verify context is closed after dispose
            deviceList.Dispose();
            context.Dispose();
            context.IsClosed.Should().BeTrue();
        });
    }
};
