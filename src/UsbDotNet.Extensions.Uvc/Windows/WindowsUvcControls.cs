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
    private readonly SafeVideoDeviceHandle _handle;
    private readonly ConcurrentDictionary<Guid, uint> _extensionUnitNodeIds = new();

    internal WindowsUvcControls(SafeVideoDeviceHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the current value and flags of a camera terminal control property.
    /// </summary>
    /// <param name="cameraControl">The camera control property to query.</param>
    /// <param name="value">Receives the current property value.</param>
    /// <param name="flags">Receives the current auto/manual mode.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMCameraControl.
    /// </exception>
    public void GetCameraControl(
        UvcCameraControl cameraControl,
        out int value,
        out ControlFlags flags
    )
    {
        var cameraControlInterface = QueryInterface<IAMCameraControl>(_handle);
        try
        {
            var hr = cameraControlInterface.Get((int)cameraControl, out value, out var rawFlags);
            Marshal.ThrowExceptionForHR(hr);
            flags = (ControlFlags)rawFlags;
        }
        finally
        {
            Marshal.ReleaseComObject(cameraControlInterface);
        }
    }

    /// <summary>
    /// Sets the value of a camera terminal control property.
    /// </summary>
    /// <param name="cameraControl">The camera control property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="flags">
    /// Auto or manual mode. Defaults to <see cref="ControlFlags.Manual"/>.
    /// </param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMCameraControl.
    /// </exception>
    public void SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        ControlFlags flags = ControlFlags.Manual
    )
    {
        var cameraControlInterface = QueryInterface<IAMCameraControl>(_handle);
        try
        {
            var hr = cameraControlInterface.Set((int)cameraControl, value, (int)flags);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            Marshal.ReleaseComObject(cameraControlInterface);
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
    /// <param name="capsFlags">Receives the supported modes (auto/manual).</param>
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
        out ControlFlags capsFlags
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
            capsFlags = (ControlFlags)rawFlags;
        }
        finally
        {
            Marshal.ReleaseComObject(cameraControlInterface);
        }
    }

    /// <summary>
    /// Gets the current value and flags of a video processing amplifier property.
    /// </summary>
    /// <param name="imageSetting">The video proc amp property to query.</param>
    /// <param name="value">Receives the current property value.</param>
    /// <param name="flags">Receives the current auto/manual mode.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMVideoProcAmp.
    /// </exception>
    public void GetImageSetting(UvcImageSetting imageSetting, out int value, out ControlFlags flags)
    {
        var videoProcAmp = QueryInterface<IAMVideoProcAmp>(_handle);
        try
        {
            var hr = videoProcAmp.Get((int)imageSetting, out value, out var rawFlags);
            Marshal.ThrowExceptionForHR(hr);
            flags = (ControlFlags)rawFlags;
        }
        finally
        {
            Marshal.ReleaseComObject(videoProcAmp);
        }
    }

    /// <summary>
    /// Sets the value of a video processing amplifier property.
    /// </summary>
    /// <param name="imageSetting">The video proc amp property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="flags">
    /// Auto or manual mode. Defaults to <see cref="ControlFlags.Manual"/>.
    /// </param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support IAMVideoProcAmp.
    /// </exception>
    public void SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        ControlFlags flags = ControlFlags.Manual
    )
    {
        var videoProcAmpInterface = QueryInterface<IAMVideoProcAmp>(_handle);
        try
        {
            var hr = videoProcAmpInterface.Set((int)imageSetting, value, (int)flags);
            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            Marshal.ReleaseComObject(videoProcAmpInterface);
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
        out ControlFlags capsFlags
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
            capsFlags = (ControlFlags)rawFlags;
        }
        finally
        {
            Marshal.ReleaseComObject(videoProcAmpInterface);
        }
    }

    /// <summary>
    /// Reads data from a UVC Extension Unit control via Kernel Streaming;
    /// assuming the device has only one unit/node with the given extension GUID.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes actually returned by the device.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnit(Guid extensionGuid, uint xuControl, Span<byte> data)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid);
        return GetExtensionUnit(extensionGuid, nodeId, xuControl, data);
    }

    /// <summary>
    /// Writes data to a UVC Extension Unit control via Kernel Streaming;
    /// assuming the device has only one unit/node with the given extension GUID.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The control data to write to the device.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public void SetExtensionUnit(Guid extensionGuid, uint xuControl, ReadOnlySpan<byte> data)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid);
        SetExtensionUnit(extensionGuid, nodeId, xuControl, data);
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming;
    /// assuming the device has only one unit/node with the given extension GUID.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="COMException">
    /// The device returned an error (other than ERROR_MORE_DATA).
    /// </exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnitLength(Guid extensionGuid, uint xuControl)
    {
        var nodeId = GetCachedExtensionUnitNodeId(extensionGuid);
        return GetExtensionUnitLength(extensionGuid, nodeId, xuControl);
    }

    private uint GetCachedExtensionUnitNodeId(Guid extensionGuid) =>
        _extensionUnitNodeIds.GetOrAdd(
            extensionGuid,
            guid =>
                GetExtensionUnitNodeId(_handle, guid)
                ?? throw new InvalidOperationException(
                    $"Extension unit is not supported because no matching node ID was found in the IKsTopologyInfo DirectShow filter."
                )
        );

    /// <summary>
    /// Finds the Kernel Streaming topology node ID for a UVC Extension Unit by its GUID.
    /// </summary>
    /// <remarks>
    /// Uses <c>IKsTopologyInfo</c> to enumerate all nodes in the DirectShow filter.
    /// For UVC extension units the node type GUID equals the extension unit's
    /// <c>guidExtensionCode</c>, so the matching node index is the <c>entityId</c>
    /// required by <see cref="GetExtensionUnit(Guid, uint, Span{byte})"/>,
    /// <see cref="SetExtensionUnit(Guid, uint, ReadOnlySpan{byte})"/>,
    /// and <see cref="GetExtensionUnitLength(Guid, uint)"/>.
    /// </remarks>
    /// <param name="handle">A valid <see cref="SafeVideoDeviceHandle"/>.</param>
    /// <param name="extensionGuid">The extension unit GUID to search for.</param>
    /// <returns>
    /// The zero-based node index, or null if no node with the given GUID was found.
    /// </returns>
    /// <exception cref="COMException">
    /// The device returned an error from <c>IKsTopologyInfo</c>.
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// The device does not support <c>IKsTopologyInfo</c>.
    /// </exception>
    private static uint? GetExtensionUnitNodeId(SafeVideoDeviceHandle handle, Guid extensionGuid)
    {
        var topologyInfoInterface = QueryInterface<IKsTopologyInfo>(handle);
        try
        {
            var hr = topologyInfoInterface.get_NumNodes(out var numNodes);
            Marshal.ThrowExceptionForHR(hr);

            for (uint i = 0; i < numNodes; i++)
            {
                hr = topologyInfoInterface.get_NodeType(i, out var nodeType);
                Marshal.ThrowExceptionForHR(hr);

                if (nodeType == extensionGuid)
                    return i;
            }

            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(topologyInfoInterface);
        }
    }

    /// <summary>
    /// Reads data from a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="entityId">The topology node ID of the extension unit.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes actually returned by the device.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnit(Guid extensionGuid, uint entityId, uint xuControl, Span<byte> data)
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = xuControl,
                Flags = KsPropertyFlags.Get | KsPropertyFlags.Topology,
            },
            NodeId = entityId,
        };

        var buffer = new byte[data.Length];
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ksControl = QueryInterface<IKsControl>(_handle);
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
                Marshal.ReleaseComObject(ksControl);
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
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="entityId">The topology node ID of the extension unit.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The control data to write to the device.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public void SetExtensionUnit(
        Guid extensionGuid,
        uint entityId,
        uint xuControl,
        ReadOnlySpan<byte> data
    )
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = xuControl,
                Flags = KsPropertyFlags.Set | KsPropertyFlags.Topology,
            },
            NodeId = entityId,
        };

        var buffer = data.ToArray();
        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            var ksControl = QueryInterface<IKsControl>(_handle);
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
                Marshal.ReleaseComObject(ksControl);
            }
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="entityId">The topology node ID of the extension unit.</param>
    /// <param name="xuControl">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="COMException">
    /// The device returned an error (other than ERROR_MORE_DATA).
    /// </exception>
    /// <exception cref="InvalidCastException">The device does not support IKsControl.</exception>
    public int GetExtensionUnitLength(Guid extensionGuid, uint entityId, uint xuControl)
    {
        var node = new KspNode
        {
            Property = new KsProperty
            {
                Set = extensionGuid,
                Id = xuControl,
                Flags = KsPropertyFlags.Get | KsPropertyFlags.Topology,
            },
            NodeId = entityId,
        };

        var ksControl = QueryInterface<IKsControl>(_handle);
        try
        {
            var hr = ksControl.KsProperty(
                ref node,
                Marshal.SizeOf<KspNode>(),
                IntPtr.Zero,
                0,
                out var bytesReturned
            );

            // HRESULT_FROM_WIN32(ERROR_MORE_DATA) = 0x800700EA is expected here.
            const int errorMoreData = unchecked((int)0x800700EA);
            if (hr != 0 && hr != errorMoreData)
                Marshal.ThrowExceptionForHR(hr);

            return bytesReturned;
        }
        finally
        {
            Marshal.ReleaseComObject(ksControl);
        }
    }

    private static T QueryInterface<T>(SafeVideoDeviceHandle handle)
        where T : class
    {
        if (handle.IsInvalid || handle.IsClosed)
            throw new ObjectDisposedException(nameof(SafeVideoDeviceHandle));

        return (T)Marshal.GetObjectForIUnknown(handle.DangerousGetHandle());
    }

    public void Dispose() => _handle.Dispose();
}
