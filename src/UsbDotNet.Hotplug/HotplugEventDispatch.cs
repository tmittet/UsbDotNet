using Microsoft.Extensions.Logging;

namespace UsbDotNet.Hotplug;

internal static class HotplugEventDispatch
{
    /// <summary>
    /// Invokes each subscriber of <paramref name="handler"/> individually so that one throwing
    /// handler can't stop delivery to the others, nor let exceptions escape onto the caller thread.
    /// </summary>
    public static void RaiseSafely<TArgs>(
        EventHandler<TArgs>? handler,
        ILogger logger,
        object sender,
        TArgs args,
        string deviceKey
    )
    {
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<TArgs>>())
        {
            try
            {
                subscriber(sender, args);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // A handler is user code; whatever it throws must not reach the dispatching thread
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                logger.LogWarning(
                    ex,
                    "Event handler '{Handler}' threw for device '{DeviceKey}'. {ErrorType}: {ErrorMessage}",
                    $"{subscriber.Method.DeclaringType?.Name}.{subscriber.Method.Name}",
                    deviceKey,
                    ex.GetType().Name,
                    ex.Message
                );
            }
        }
    }
}
