using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// Windows UVC control implementation using DirectShow and Kernel Streaming COM interfaces.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsUvcControls : IUvcControls
{
    private const int ErrorInsufficientBuffer = unchecked((int)0x8007007A);
    private const int ErrorMoreData = unchecked((int)0x800700EA);
    public static readonly Guid DeviceSpecificNode = new("941C7AC0-C559-11D0-8A2B-00A0C9255AC1");

    private readonly SafeVideoDeviceHandle _handle;
    private readonly ConcurrentDictionary<Guid, uint> _extensionUnitNodeIds = new();

    internal WindowsUvcControls(SafeVideoDeviceHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the current value and control type of a camera terminal control property.
    /// </summary>
    /// <param name="cameraControl">The camera control property to query.</param>
    /// <param name="controlType">The current auto/manual mode.</param>
    /// <returns>the current property value.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMCameraControl.
    /// </exception>
    public int GetCameraControl(UvcCameraControl cameraControl, out UvcControlType controlType)
    {
        var cameraControlInterface = QueryInterface<IAMCameraControl>(_handle);
        try
        {
            var hr = cameraControlInterface.Get(
                (int)cameraControl,
                out var value,
                out var rawFlags
            );
            Marshal.ThrowExceptionForHR(hr);
            controlType = (UvcControlType)rawFlags;
            return value;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(cameraControlInterface);
        }
    }

    /// <summary>
    /// Sets the value of a camera terminal control property.
    /// </summary>
    /// <param name="cameraControl">The camera control property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">
    /// Auto or manual mode. Defaults to <see cref="UvcControlType.Manual"/>.
    /// </param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMCameraControl.
    /// </exception>
    public void SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        var cameraControlInterface = QueryInterface<IAMCameraControl>(_handle);
        try
        {
            var hr = cameraControlInterface.Set((int)cameraControl, value, (int)controlType);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            _ = Marshal.ReleaseComObject(cameraControlInterface);
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a camera control property.
    /// </summary>
    /// <param name="cameraControl">The camera control property to query.</param>
    /// <param name="minValue">Receives the minimum supported value.</param>
    /// <param name="maxValue">Receives the maximum supported value.</param>
    /// <param name="stepSize">Receives the stepping delta between valid values.</param>
    /// <param name="defaultValue">Receives the default value.</param>
    /// <param name="controlType">Receives the supported modes (auto/manual).</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMCameraControl.
    /// </exception>
    public void GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType controlType
    )
    {
        var cameraControlInterface = QueryInterface<IAMCameraControl>(_handle);
        try
        {
            var hr = cameraControlInterface.GetRange(
                (int)cameraControl,
                out minValue,
                out maxValue,
                out stepSize,
                out defaultValue,
                out var rawFlags
            );
            Marshal.ThrowExceptionForHR(hr);
            controlType = (UvcControlType)rawFlags;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(cameraControlInterface);
        }
    }

    /// <summary>
    /// Gets the current value and control type of a video processing amplifier property.
    /// </summary>
    /// <param name="imageSetting">The video proc amp property to query.</param>
    /// <param name="controlType">The current auto/manual mode.</param>
    /// <returns>the current property value.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMVideoProcAmp.
    /// </exception>
    public int GetImageSetting(UvcImageSetting imageSetting, out UvcControlType controlType)
    {
        var videoProcAmp = QueryInterface<IAMVideoProcAmp>(_handle);
        try
        {
            var hr = videoProcAmp.Get((int)imageSetting, out var value, out var rawFlags);
            Marshal.ThrowExceptionForHR(hr);
            controlType = (UvcControlType)rawFlags;
            return value;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(videoProcAmp);
        }
    }

    /// <summary>
    /// Sets the value of a video processing amplifier property.
    /// </summary>
    /// <param name="imageSetting">The video proc amp property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="controlType">
    /// Auto or manual mode. Defaults to <see cref="UvcControlType.Manual"/>.
    /// </param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMVideoProcAmp.
    /// </exception>
    public void SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        var videoProcAmpInterface = QueryInterface<IAMVideoProcAmp>(_handle);
        try
        {
            var hr = videoProcAmpInterface.Set((int)imageSetting, value, (int)controlType);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            _ = Marshal.ReleaseComObject(videoProcAmpInterface);
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a video proc amp property.
    /// </summary>
    /// <param name="imageSetting">The video proc amp property to query.</param>
    /// <param name="minValue">Receives the minimum supported value.</param>
    /// <param name="maxValue">Receives the maximum supported value.</param>
    /// <param name="stepSize">Receives the stepping delta between valid values.</param>
    /// <param name="defaultValue">Receives the default value.</param>
    /// <param name="capsFlags">Receives the supported modes (auto/manual).</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMVideoProcAmp.
    /// </exception>
    public void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    )
    {
        var videoProcAmpInterface = QueryInterface<IAMVideoProcAmp>(_handle);
        try
        {
            var hr = videoProcAmpInterface.GetRange(
                (int)imageSetting,
                out minValue,
                out maxValue,
                out stepSize,
                out defaultValue,
                out var rawFlags
            );
            Marshal.ThrowExceptionForHR(hr);
            capsFlags = (UvcControlType)rawFlags;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(videoProcAmpInterface);
        }
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="COMException">
    /// The device returned an error (other than ERROR_MORE_DATA).
    /// </exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnitLength(Guid extensionGuid, uint control)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
        return GetExtensionUnitLength(_handle, extensionGuid, nodeId, control);
    }

    /// <summary>Reads data from a UVC Extension Unit control via Kernel Streaming.</summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes actually returned by the device.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnit(Guid extensionGuid, uint control, Span<byte> data)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
        return GetExtensionUnit(_handle, extensionGuid, nodeId, control, data);
    }

    /// <summary>Writes data to a UVC Extension Unit control via Kernel Streaming.</summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The control data to write to the device.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public void SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
        SetExtensionUnit(_handle, extensionGuid, nodeId, control, data);
    }

    private static T QueryInterface<T>(SafeVideoDeviceHandle handle)
        where T : class =>
        handle.IsInvalid || handle.IsClosed
            ? throw new ObjectDisposedException(nameof(SafeVideoDeviceHandle))
            : (T)Marshal.GetObjectForIUnknown(handle.DangerousGetHandle());

    private uint GetCachedExtensionUnitNodeId(Guid extensionGuid, uint control) =>
        _extensionUnitNodeIds.GetOrAdd(
            extensionGuid,
            guid =>
                GetExtensionUnitNodeId(guid, control)
                ?? throw new InvalidOperationException(
                    $"Extension unit is not supported; no node ID accepting "
                        + $"control 0x{control:X8} for extension unit {guid} was found."
                )
        );

    /// <summary>
    /// Tries to find the topology node ID for a UVC Extension Unit that supports the specified control.
    /// </summary>
    private uint? GetExtensionUnitNodeId(Guid extensionGuid, uint control)
    {
        foreach (var nodeId in GetExtensionUnitNodeIds(_handle))
        {
            try
            {
                _ = GetExtensionUnitLength(_handle, extensionGuid, nodeId, control);
                return nodeId;
            }
            catch (COMException)
            {
                // Ignore
            }
        }
        return null;
    }

    /// <summary>
    /// Finds all Kernel Streaming topology node IDs for a UVC Extension Unit.
    /// </summary>
    /// <remarks>
    /// Uses <c>IKsTopologyInfo</c> to enumerate all nodes in the DirectShow filter.
    /// </remarks>
    /// <param name="handle">A valid <see cref="SafeVideoDeviceHandle"/>.</param>
    /// <returns>
    /// A list of all the node <see cref="DeviceSpecificNode"/> node IDs.
    /// </returns>
    /// <exception cref="COMException">
    /// The device returned an error from <c>IKsTopologyInfo</c>.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support <c>IKsTopologyInfo</c>.
    /// </exception>
    private static List<uint> GetExtensionUnitNodeIds(SafeVideoDeviceHandle handle)
    {
        var topology = QueryInterface<IKsTopologyInfo>(handle);
        var ksControl = QueryInterface<IKsControl>(handle);
        var xuNodes = new List<uint>();
        try
        {
            var hr = topology.get_NumNodes(out var numNodes);
            Marshal.ThrowExceptionForHR(hr);

            for (uint nodeId = 0; nodeId < numNodes; nodeId++)
            {
                hr = topology.get_NodeType(nodeId, out var nodeType);
                Marshal.ThrowExceptionForHR(hr);

                if (nodeType == DeviceSpecificNode)
                {
                    xuNodes.Add(nodeId);
                }
            }
        }
        finally
        {
            _ = Marshal.ReleaseComObject(ksControl);
            _ = Marshal.ReleaseComObject(topology);
        }
        return xuNodes;
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    /// <param name="handle">A valid <see cref="SafeVideoDeviceHandle"/>.</param>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="COMException">
    /// The device returned an error (other than ERROR_MORE_DATA).
    /// </exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    private static int GetExtensionUnitLength(
        SafeVideoDeviceHandle handle,
        Guid extensionGuid,
        uint nodeId,
        uint control
    )
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = control,
                Flags = KsPropertyFlags.Get | KsPropertyFlags.Topology,
            },
            NodeId = nodeId,
        };

        var ksControl = QueryInterface<IKsControl>(handle);
        try
        {
            var hr = ksControl.KsProperty(
                ref node,
                Marshal.SizeOf<KspNode>(),
                IntPtr.Zero,
                0,
                out var bytesReturned
            );

            // ErrorMoreData (0x800700EA) and ErrorInsufficientBuffer (0x8007007A) expected
            if (hr is not 0 and not ErrorMoreData and not ErrorInsufficientBuffer)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return bytesReturned;
        }
        finally
        {
            _ = Marshal.ReleaseComObject(ksControl);
        }
    }

    /// <summary>
    /// Reads data from a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    /// <param name="handle">A valid <see cref="SafeVideoDeviceHandle"/>.</param>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes actually returned by the device.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    private static int GetExtensionUnit(
        SafeVideoDeviceHandle handle,
        Guid extensionGuid,
        uint nodeId,
        uint control,
        Span<byte> data
    )
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = control,
                Flags = KsPropertyFlags.Get | KsPropertyFlags.Topology,
            },
            NodeId = nodeId,
        };

        var buffer = new byte[data.Length];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ksControl = QueryInterface<IKsControl>(handle);
            try
            {
                var hr = ksControl.KsProperty(
                    ref node,
                    Marshal.SizeOf<KspNode>(),
                    pinned.AddrOfPinnedObject(),
                    buffer.Length,
                    out var bytesReturned
                );
                Marshal.ThrowExceptionForHR(hr);
                buffer.AsSpan(0, bytesReturned).CopyTo(data);
                return bytesReturned;
            }
            finally
            {
                _ = Marshal.ReleaseComObject(ksControl);
            }
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// Writes data to a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    /// <param name="handle">A valid <see cref="SafeVideoDeviceHandle"/>.</param>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The control data to write to the device.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    private static void SetExtensionUnit(
        SafeVideoDeviceHandle handle,
        Guid extensionGuid,
        uint nodeId,
        uint control,
        ReadOnlySpan<byte> data
    )
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = control,
                Flags = KsPropertyFlags.Set | KsPropertyFlags.Topology,
            },
            NodeId = nodeId,
        };

        var buffer = data.ToArray();
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ksControl = QueryInterface<IKsControl>(handle);
            try
            {
                var hr = ksControl.KsProperty(
                    ref node,
                    Marshal.SizeOf<KspNode>(),
                    pinned.AddrOfPinnedObject(),
                    buffer.Length,
                    out _
                );
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                _ = Marshal.ReleaseComObject(ksControl);
            }
        }
        finally
        {
            pinned.Free();
        }
    }

    public void Dispose() => _handle.Dispose();
}
