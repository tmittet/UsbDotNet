using Microsoft.Extensions.Logging;

namespace UsbDotNet.Internal;

internal static class EventDispatch
{
    /// <summary>
    /// Invokes each subscriber of <paramref name="handler"/> individually so that one throwing
    /// handler can't stop delivery to the others, nor let exceptions escape onto the caller thread.
    /// </summary>
    public static void RaiseSafely(EventHandler? handler, ILogger logger, object sender)
    {
        if (handler is null)
            return;

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(sender, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Event handler '{Handler}' threw. {ErrorType}: {ErrorMessage}",
                    $"{subscriber.Method.DeclaringType?.Name}.{subscriber.Method.Name}",
                    ex.GetType().Name,
                    ex.Message
                );
            }
        }
    }

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
            return;

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<TArgs>>())
        {
            try
            {
                subscriber(sender, args);
            }
            catch (Exception ex)
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
