namespace UsbDotNet;

public sealed partial class Usb
{
    private enum DisposeState
    {
        Live,
        Disposing,
        Disposed,
    }
}
