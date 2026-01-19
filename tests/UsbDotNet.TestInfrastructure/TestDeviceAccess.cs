namespace UsbDotNet.TestInfrastructure;

[Flags]
public enum TestDeviceAccess
{
    None = 0,
    Control = 0b0001,
    BulkRead = 0b0010,
    BulkWrite = 0b0100,
}
