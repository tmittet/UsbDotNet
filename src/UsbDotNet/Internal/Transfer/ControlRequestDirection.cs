namespace UsbDotNet.Internal.Transfer;

/// <summary>
/// This enum represents the control request direction. The enum value should be bitwise combined
/// with ControlRequestType and ControlRequestRecipient, to form the full ControlRequest type byte.
/// </summary>
internal enum ControlRequestDirection : byte
{
    Out = 0,
    In = 1,
}
