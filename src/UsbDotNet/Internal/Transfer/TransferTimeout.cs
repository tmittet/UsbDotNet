namespace UsbDotNet.Internal.Transfer;

/// <summary>
/// Validation of the timeout argument, shared by all transfer types.
/// </summary>
internal static class TransferTimeout
{
    /// <summary>
    /// Throw ArgumentOutOfRangeException when timeout is 0 or less than -1. A timeout is a number
    /// of milliseconds greater than 0, or -1 (<see cref="Timeout.Infinite"/>) for no timeout.
    /// </summary>
    internal static void ThrowIfInvalid(int timeout)
    {
        if (timeout is 0 or < Timeout.Infinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Invalid timeout; must be greater than 0 or -1 (infinite)."
            );
        }
    }
}
