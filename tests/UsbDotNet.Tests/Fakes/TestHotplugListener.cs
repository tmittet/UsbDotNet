using UsbDotNet.Descriptor;
using UsbDotNet.Internal;

namespace UsbDotNet.Tests.Fakes;

/// <summary>
/// Forwarding <see cref="IHotplugListener"/> for tests; callbacks left unset are no-ops.
/// </summary>
internal sealed class TestHotplugListener : IHotplugListener
{
    public Action<IUsbDeviceDescriptor>? DeviceArrived { get; init; }
    public Action<IUsbDeviceDescriptor>? DeviceLeft { get; init; }
    public Action? ProviderDisposed { get; init; }

    public void OnDeviceArrived(IUsbDeviceDescriptor descriptor) =>
        DeviceArrived?.Invoke(descriptor);

    public void OnDeviceLeft(IUsbDeviceDescriptor descriptor) => DeviceLeft?.Invoke(descriptor);

    public void OnProviderDisposed() => ProviderDisposed?.Invoke();
}
