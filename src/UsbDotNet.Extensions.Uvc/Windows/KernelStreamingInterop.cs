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
/// Describes a connection between two nodes in a KS filter topology.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KSTopologyConnection
{
    public uint FromNode;
    public uint FromNodePin;
    public uint ToNode;
    public uint ToNodePin;
}

/// <summary>
/// DirectShow/KS topology information interface for enumerating filter nodes.
/// Used to discover the topology node ID of a UVC Extension Unit by its GUID.
/// </summary>
[ComImport]
[Guid("720D4AC0-7533-11D0-A5D6-28DB04C10000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#pragma warning disable IDE1006 // Naming Styles
internal interface IKsTopologyInfo
{
    [PreserveSig]
    int get_NumCategories(out uint pdwNumCategories);

    [PreserveSig]
    int get_Category(uint dwIndex, out Guid pCategory);

    [PreserveSig]
    int get_NumConnections(out uint pdwNumConnections);

    [PreserveSig]
    int get_ConnectionInfo(uint dwIndex, out KSTopologyConnection pConnectionInfo);

    [PreserveSig]
    int get_NodeName(uint dwNodeId, [MarshalAs(UnmanagedType.BStr)] out string pbstrNodeName);

    [PreserveSig]
    int get_NumNodes(out uint pdwNumNodes);

    [PreserveSig]
    int get_NodeType(uint dwNodeId, out Guid pNodeType);

    [PreserveSig]
    int CreateNodeInstance(
        uint dwNodeId,
        [In] ref Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject
    );
}
#pragma warning restore IDE1006 // Naming Styles

/// <summary>
/// Filter-scoped IKsControl. Use this on the filter object when sending topology-routed requests with KSP_NODE.
/// </summary>
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

    [PreserveSig]
    int KsMethod(
        IntPtr method,
        uint methodLength,
        IntPtr methodData,
        uint dataLength,
        out uint bytesReturned
    );

    [PreserveSig]
    int KsEvent(
        IntPtr @event,
        uint eventLength,
        IntPtr eventData,
        uint dataLength,
        out uint bytesReturned
    );
}
