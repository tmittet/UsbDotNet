namespace UsbDotNet.Core;

public class UsbException : Exception
{
    public UsbResult Code { get; }

    public UsbException(UsbResult code, string message)
        : base(message)
    {
        Code = code;
    }
}
