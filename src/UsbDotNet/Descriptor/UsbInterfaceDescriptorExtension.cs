namespace UsbDotNet.Descriptor;

/// <summary>
/// Extension methods for IUsbInterfaceDescriptor.
/// </summary>
public static class UsbInterfaceDescriptorExtension
{
    /// <summary>
    /// Returns the endpoints with the specified direction, in descriptor order.
    /// </summary>
    /// <param name="descriptor">The USB interface descriptor.</param>
    /// <param name="direction">The desired USB endpoint direction.</param>
    /// <returns>The matching endpoints; empty when the interface has none.</returns>
    public static IReadOnlyList<IUsbEndpointDescriptor> GetEndpoints(
        this IUsbInterfaceDescriptor descriptor,
        UsbEndpointDirection direction
    ) => [.. descriptor.Endpoints.Where(e => e.EndpointAddress.Direction == direction)];
}
