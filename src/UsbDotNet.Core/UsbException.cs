namespace UsbDotNet.Core;

public class UsbException : Exception
{
    public UsbResult Code { get; }

    public UsbException(UsbResult code, string message)
        : base(message)
    {
        Code = code;
    }

    public UsbException()
        : this(null) { }

    public UsbException(string? message)
        : this(message, null) { }

    public UsbException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        Code = UsbResult.UnknownError;
    }
}
