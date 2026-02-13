namespace UsbDotNet.Descriptor;

/// <inheritdoc/>
public readonly record struct UsbDeviceDescriptor : IUsbDeviceDescriptor
{
    /// <inheritdoc/>
    public string DeviceKey { get; init; }

    /// <inheritdoc/>
    public ushort BcdUsb { get; init; }

    /// <inheritdoc/>
    public UsbClass DeviceClass { get; init; }

    /// <inheritdoc/>
    public byte DeviceSubClass { get; init; }

    /// <inheritdoc/>
    public byte DeviceProtocol { get; init; }

    /// <inheritdoc/>
    public byte MaxPacketSize0 { get; init; }

    /// <inheritdoc/>
    public ushort VendorId { get; init; }

    /// <inheritdoc/>
    public ushort ProductId { get; init; }

    /// <inheritdoc/>
    public ushort BcdDevice { get; init; }

    /// <inheritdoc/>
    public byte ManufacturerIndex { get; init; }

    /// <inheritdoc/>
    public byte ProductIndex { get; init; }

    /// <inheritdoc/>
    public byte SerialNumberIndex { get; init; }

    /// <inheritdoc/>
    public byte NumConfigurations { get; init; }

    /// <inheritdoc/>
    public byte BusNumber { get; init; }

    /// <inheritdoc/>
    public byte BusAddress { get; init; }

    /// <inheritdoc/>
    public byte PortNumber { get; init; }

    /// <summary>
    /// Create a string device key.
    /// </summary>
    public static string GetKey(ushort vendorId, ushort productId, byte busNumber, byte busAddress)
    {
        return $"{vendorId:X4}_{productId:X4}_{busNumber}_{busAddress}";
    }

    internal UsbDeviceDescriptor(
        UsbDotNet.LibUsbNative.Structs.libusb_device_descriptor partialDescriptor,
        byte busNumber,
        byte address,
        byte portNumber
    )
    {
        BcdUsb = partialDescriptor.bcdUSB;
        DeviceClass = (UsbClass)partialDescriptor.bDeviceClass;
        DeviceSubClass = partialDescriptor.bDeviceSubClass;
        DeviceProtocol = partialDescriptor.bDeviceProtocol;
        MaxPacketSize0 = partialDescriptor.bMaxPacketSize0;
        VendorId = partialDescriptor.idVendor;
        ProductId = partialDescriptor.idProduct;
        BcdDevice = partialDescriptor.bcdDevice;
        ManufacturerIndex = partialDescriptor.iManufacturer;
        ProductIndex = partialDescriptor.iProduct;
        SerialNumberIndex = partialDescriptor.iSerialNumber;
        NumConfigurations = partialDescriptor.bNumConfigurations;
        BusNumber = busNumber;
        BusAddress = address;
        PortNumber = portNumber;

        DeviceKey = GetKey(VendorId, ProductId, BusNumber, BusAddress);
    }

    public override string ToString() => DeviceKey;
}
