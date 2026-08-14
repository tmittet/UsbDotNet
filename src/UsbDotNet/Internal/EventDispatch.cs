using Microsoft.Extensions.Logging;

namespace UsbDotNet.Internal;

internal static class EventDispatch
{
    /// <summary>
    /// Invokes each target of <paramref name="callback"/> individually so that one throwing
    /// target can't stop delivery to the others, nor let exceptions escape onto the caller thread.
    /// </summary>
    public static void RaiseSafely(Action? callback, ILogger logger)
    {
        if (callback is null)
            return;

        foreach (var target in callback.GetInvocationList().Cast<Action>())
        {
            try
            {
                target();
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Callback '{Callback}' threw. {ErrorType}: {ErrorMessage}",
                    $"{target.Method.DeclaringType?.Name}.{target.Method.Name}",
                    ex.GetType().Name,
                    ex.Message
                );
            }
        }
    }

    /// <summary>
    /// Invokes each target of <paramref name="callback"/> individually so that one throwing
    /// target can't stop delivery to the others, nor let exceptions escape onto the caller thread.
    /// </summary>
    public static void RaiseSafely<TArgs>(
        Action<TArgs>? callback,
        ILogger logger,
        TArgs args,
        string deviceKey
    )
    {
        if (callback is null)
            return;

        foreach (var target in callback.GetInvocationList().Cast<Action<TArgs>>())
        {
            try
            {
                target(args);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Callback '{Callback}' threw for device '{DeviceKey}'. {ErrorType}: {ErrorMessage}",
                    $"{target.Method.DeclaringType?.Name}.{target.Method.Name}",
                    deviceKey,
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
