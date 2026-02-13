using System.Collections.Immutable;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;

namespace UsbDotNet;

/// <summary>
/// Extension methods for IUsbDevice.
/// </summary>
public static class UsbDeviceExtension
{
    /// <summary>
    /// Claim a USB interface. The interface will be auto-released when the device is disposed.
    /// <para>
    /// NOTE: When more than one matching interface is found, the first interface found,
    /// ordered by interface number and alternate setting, is selected and claimed.
    /// </para>
    /// </summary>
    /// <param name="device">A UsbDevice instance.</param>
    /// <param name="withClass">Interface class filter.</param>
    /// <param name="withSubClass">Optional interface sub-class filter.</param>
    /// <param name="withProtocol">Optional interface protocol filter.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the USB interface is already claimed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a USB interface of the provided class or optional protocol is not found.
    /// </exception>
    /// <exception cref="UsbException">
    /// Thrown when the USB interface claim operation fails.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the UsbDevice is disposed.
    /// </exception>
    public static IUsbInterface ClaimInterface(
        this IUsbDevice device,
        UsbClass withClass,
        byte? withSubClass = null,
        byte? withProtocol = null
    )
    {
        var usbInterface = device
            .GetInterfaceDescriptorList(withClass, withSubClass, withProtocol)
            .OrderBy(i => i.InterfaceNumber)
            .ThenBy(i => i.AlternateSetting)
            .FirstOrDefault();
        return usbInterface is null
            ? throw new InvalidOperationException(
                $"Device '{device}' {withClass} interface not found."
            )
            : device.ClaimInterface(usbInterface);
    }

    /// <summary>
    /// Get a flat list of interface descriptors and alternate settings matching given parameters.
    /// </summary>
    /// <param name="device">A UsbDevice instance.</param>
    /// <param name="withClass">Interface class filter.</param>
    /// <param name="withSubClass">Optional interface sub-class filter.</param>
    /// <param name="withProtocol">Optional interface protocol filter.</param>
    /// <returns>A list of matching interface descriptors.</returns>
    public static IReadOnlyCollection<IUsbInterfaceDescriptor> GetInterfaceDescriptorList(
        this IUsbDevice device,
        UsbClass withClass,
        byte? withSubClass = null,
        byte? withProtocol = null
    ) =>
        // Flatten the interface descriptors
        device
            .ConfigDescriptor.Interfaces.SelectMany(i => i.Value.Values)
            // Apply filters
            .Where(i =>
                i.InterfaceClass == withClass
                && (withSubClass is null || i.InterfaceSubClass == withSubClass.Value)
                && (withProtocol is null || i.InterfaceProtocol == withProtocol.Value)
            )
            .ToImmutableList();

    /// <summary>
    /// Get interface descriptors matching given parameters.
    /// </summary>
    /// <param name="device">A UsbDevice instance.</param>
    /// <param name="withClass">Interface class filter.</param>
    /// <param name="withSubClass">Optional interface sub-class filter.</param>
    /// <param name="withProtocol">Optional interface protocol filter.</param>
    /// <returns>
    /// A dictionary of USB interface descriptors grouped by interface number. For each interface
    /// number, the value is a dictionary of alternate interface descriptors keyed by the alternate
    /// setting number. Per the USB spec, alternate setting 0 always exists and is the default
    /// alternate setting for each device configuration.
    /// </returns>
    public static IDictionary<
        byte,
        IDictionary<byte, IUsbInterfaceDescriptor>
    > GetInterfaceDescriptors(
        this IUsbDevice device,
        UsbClass withClass,
        byte? withSubClass = null,
        byte? withProtocol = null
    ) => device.GetInterfaceDescriptorList(withClass, withSubClass, withProtocol).Regroup();

    /// <summary>
    /// Check if device has an interface matching given parameters.
    /// </summary>
    /// <param name="device">A UsbDevice instance.</param>
    /// <param name="withClass">Interface class filter.</param>
    /// <param name="withSubClass">Optional interface sub-class filter.</param>
    /// <param name="withProtocol">Optional interface protocol filter.</param>
    /// <returns>True when one or more matching interfaces are found.</returns>
    public static bool HasInterface(
        this IUsbDevice device,
        UsbClass withClass,
        byte? withSubClass = null,
        byte? withProtocol = null
    ) => device.GetInterfaceDescriptorList(withClass, withSubClass, withProtocol).Count != 0;

    /// <summary>
    /// Regroup interface descriptors into a nested dictionary of USB interface
    /// descriptors grouped by interface number and alternate setting number.
    /// </summary>
    private static ImmutableDictionary<byte, IDictionary<byte, IUsbInterfaceDescriptor>> Regroup(
        this IEnumerable<IUsbInterfaceDescriptor> descriptors
    ) =>
        descriptors
            // Regroup by interface number
            .GroupBy(i => i.InterfaceNumber)
            // Create the nested dictionary
            .ToDictionary(
                g => g.Key,
                g =>
                    (IDictionary<byte, IUsbInterfaceDescriptor>)
                        g.ToDictionary(i => i.AlternateSetting, i => i).ToImmutableDictionary()
            )
            .ToImmutableDictionary();
}
