using System.Threading.Channels;

namespace UsbDotNet.Hotplug;

/// <summary>
/// A single hotplug subscription. Read events from <see cref="Reader"/>;
/// dispose to stop receiving events and release the underlying channel.
/// </summary>
public interface IUsbHotplugSubscription : IDisposable
{
    /// <summary>
    /// The channel of hotplug events for this subscription. Completes when the subscription or its
    /// owning monitor is disposed. The channel is unbounded and events are never dropped, so a
    /// consumer that stops reading while the subscription is alive will accumulate events; dispose
    /// the subscription when done.
    /// </summary>
    ChannelReader<UsbHotplugEvent> Reader { get; }
}
