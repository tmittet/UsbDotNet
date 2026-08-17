using System.Threading.Channels;

namespace UsbDotNet.Hotplug;

/// <summary>
/// A single hotplug subscription. Read events from <see cref="Reader"/>;
/// dispose to stop receiving events and release the underlying channel.
/// </summary>
public interface IUsbHotplugSubscription : IDisposable
{
    /// <summary>
    /// The channel of hotplug events for this subscription. Completes cleanly when the
    /// subscription itself is disposed; when the owning monitor or the underlying IUsb instance
    /// is disposed, pending and future reads are canceled with an
    /// <see cref="OperationCanceledException"/> and undelivered events are dropped, since they
    /// describe devices the disposed instance can no longer reach. While the subscription is
    /// live the channel is unbounded and events are never dropped, so a consumer that stops
    /// reading will accumulate events; dispose the subscription when done.
    /// </summary>
    ChannelReader<UsbHotplugEvent> Reader { get; }
}
