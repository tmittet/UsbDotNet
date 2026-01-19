namespace UsbDotNet.Descriptor;

public static class UsbInterfaceDescriptorExtension
{
    /// <summary>
    /// Returns the first endpoint with the specified direction.
    /// </summary>
    /// <param name="descriptor">The USB endpoint descriptor.</param>
    /// <param name="direction">The desired USB endpoint direction.</param>
    /// <param name="count">The actual number of endpoints found with the given direction.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no endpoint with the specified direction is found.
    /// </exception>
    public static IUsbEndpointDescriptor GetEndpoint(
        this IUsbInterfaceDescriptor descriptor,
        UsbEndpointDirection direction,
        out int count
    )
    {
        var endpoints = descriptor
            .Endpoints.Where(e => e.EndpointAddress.Direction == direction)
            .ToList();
        count = endpoints.Count;
        return endpoints.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Interface #{descriptor.InterfaceNumber} has no {direction} endpoint."
            );
    }
}
