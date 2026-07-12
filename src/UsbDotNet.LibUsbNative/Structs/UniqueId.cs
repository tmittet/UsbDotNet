namespace UsbDotNet.LibUsbNative.Structs;

/// <summary>
/// Identity token for a native object, supporting equality and hashing.
/// Two instances are equal when they identify the same underlying native object.
/// </summary>
public readonly record struct UniqueId
{
    internal UniqueId(nint value) => Value = value;

    internal nint Value { get; }
}
