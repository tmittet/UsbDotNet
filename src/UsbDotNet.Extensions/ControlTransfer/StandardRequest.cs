using UsbDotNet.Transfer;

namespace UsbDotNet.Extensions.ControlTransfer;

/// <summary>
/// Standard control requests (USB spec Chapter 9.4) and USB 3.x additions.
/// Use in combination with ControlRead/ControlWrite <see cref="ControlRequestType.Standard"/>.
/// </summary>
public enum StandardRequest : byte
{
    GetStatus = 0x00,
    ClearFeature = 0x01,

    // 0x02 reserved
    SetFeature = 0x03,

    // 0x04 reserved
    SetAddress = 0x05,
    GetDescriptor = 0x06,
    SetDescriptor = 0x07,
    GetConfiguration = 0x08,
    SetConfiguration = 0x09,
    GetInterface = 0x0A,
    SetInterface = 0x0B,
    SynchFrame = 0x0C,

    // USB 3.x (SuperSpeed) additions:
    SetSel = 0x30,
    SetIsochDelay = 0x31,
}
