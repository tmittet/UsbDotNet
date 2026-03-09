#pragma warning disable IDE1006 // Naming Styles
// Kernel Streaming COM interface and struct definitions for extension unit controls on Windows.
// Nullable is disabled because COM interfaces don't have nullable semantics.

using System.Runtime.InteropServices;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// Kernel Streaming property request flags (KSPROPERTY_TYPE_*).
/// </summary>
[Flags]
internal enum KsPropertyFlags : uint
{
    Get = 0x00000001,
    Set = 0x00000002,
    Topology = 0x10000000,
}

/// <summary>
/// Kernel Streaming property identifier (KSPROPERTY).
/// Specifies the property set GUID, property ID, and request flags.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsProperty
{
    public Guid Set;
    public uint Id;
    public KsPropertyFlags Flags;
}

/// <summary>
/// Kernel Streaming node property (KSP_NODE).
/// Extends <see cref="KsProperty"/> with a node ID for topology-based property requests,
/// used to target a specific extension unit within the USB video function.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KspNode
{
    public KsProperty Property;
    public uint NodeId;
    public uint Reserved;
}

/// <summary>
/// DirectShow/KS topology information interface for enumerating filter nodes.
/// Used to discover the topology node ID of a UVC Extension Unit by its GUID.
/// </summary>
[ComImport]
[Guid("720D4AC0-7533-11D0-A5D6-28DB04C10000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsTopologyInfo
{
    [PreserveSig]
    int get_NumNodes(out uint numNodes);

    [PreserveSig]
    int get_NodeType(uint nodeIndex, out Guid nodeType);

    [PreserveSig]
    int get_ConnectionInfo(uint index, out KsTopologyConnection connection);

    [PreserveSig]
    int get_NodeName(
        uint nodeIndex,
        [MarshalAs(UnmanagedType.LPWStr)] out string nodeName,
        uint bufferSize,
        out uint nameLen
    );

    [PreserveSig]
    int get_NumConnections(out uint numConnections);

    [PreserveSig]
    int CreateNodeInstance(
        uint nodeIndex,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance
    );
}

/// <summary>
/// Describes a connection between two nodes in a KS filter topology.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KsTopologyConnection
{
    public uint FromNode;
    public uint FromNodePin;
    public uint ToNode;
    public uint ToNodePin;
}

[ComImport]
[Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty(
        ref KspNode property,
        int propertyLength,
        IntPtr data,
        int dataLength,
        out int bytesReturned
    );

    // KsMethod and KsEvent are not used but must be declared
    // to maintain correct vtable layout for COM interop.

    [PreserveSig]
    int KsMethod(
        IntPtr method,
        int methodLength,
        IntPtr methodData,
        int dataLength,
        out int bytesReturned
    );

    [PreserveSig]
    int KsEvent(
        IntPtr ksevent,
        int eventLength,
        IntPtr eventData,
        int dataLength,
        out int bytesReturned
    );
}
#pragma warning restore IDE1006 // Naming Styles
