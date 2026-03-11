using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using UsbDotNet.Core;

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

    private readonly IAMCameraControl? _cameraControl;
    private readonly IAMVideoProcAmp? _videoProcAmp;
    private readonly IKsControl? _ksControl;
    private readonly IKsTopologyInfo? _topologyInfo;

    private readonly object _disposeLock = new();
    private bool _disposed;

    private IAMCameraControl CameraControl =>
        !_disposed
            ? _cameraControl
                ?? throw new InvalidOperationException("IAMCameraControl not supported.")
            : throw new ObjectDisposedException(nameof(WindowsUvcControls));

    private IAMVideoProcAmp VideoProcAmp =>
        !_disposed
            ? _videoProcAmp ?? throw new InvalidOperationException("IAMVideoProcAmp not supported.")
            : throw new ObjectDisposedException(nameof(WindowsUvcControls));

    private IKsControl KsControl =>
        !_disposed
            ? _ksControl ?? throw new InvalidOperationException("IKsControl not supported.")
            : throw new ObjectDisposedException(nameof(WindowsUvcControls));

    private IKsTopologyInfo TopologyInfo =>
        !_disposed
            ? _topologyInfo ?? throw new InvalidOperationException("IKsTopologyInfo not supported.")
            : throw new ObjectDisposedException(nameof(WindowsUvcControls));

    internal WindowsUvcControls(SafeVideoDeviceHandle handle)
    {
        _handle = handle;

        var unknownDevice =
            handle.IsInvalid || handle.IsClosed
                ? throw new ObjectDisposedException(nameof(SafeVideoDeviceHandle))
                : handle.DangerousGetHandle();

        var directShowObject = Marshal.GetObjectForIUnknown(unknownDevice);

        _cameraControl = directShowObject as IAMCameraControl;
        _videoProcAmp = directShowObject as IAMVideoProcAmp;
        _ksControl = directShowObject as IKsControl;
        _topologyInfo = directShowObject as IKsTopologyInfo;
    }

    /// <summary>
    /// Gets the current value and control type of a camera terminal control property.
    /// </summary>
    public int GetCameraControl(UvcCameraControl cameraControl, out UvcControlType controlType)
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = CameraControl.Get((int)cameraControl, out var value, out var rawFlags);
                Marshal.ThrowExceptionForHR(hr);
                controlType = (UvcControlType)rawFlags;
                return value;
            }
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Sets the value of a camera terminal control property.
    /// </summary>
    public void SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = CameraControl.Set((int)cameraControl, value, (int)controlType);
                Marshal.ThrowExceptionForHR(hr);
            }
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a camera control property.
    /// </summary>
    public void GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType controlType
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = CameraControl.GetRange(
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
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Gets the current value and control type of a video processing amplifier property.
    /// </summary>
    public int GetImageSetting(UvcImageSetting imageSetting, out UvcControlType controlType)
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = VideoProcAmp.Get((int)imageSetting, out var value, out var rawFlags);
                Marshal.ThrowExceptionForHR(hr);
                controlType = (UvcControlType)rawFlags;
                return value;
            }
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Sets the value of a video processing amplifier property.
    /// </summary>
    public void SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = VideoProcAmp.Set((int)imageSetting, value, (int)controlType);
                Marshal.ThrowExceptionForHR(hr);
            }
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a video proc amp property.
    /// </summary>
    public void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            try
            {
                var hr = VideoProcAmp.GetRange(
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
            catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
            {
                throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    public int GetExtensionUnitLength(Guid extensionGuid, uint control)
    {
        try
        {
            var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
            return GetExtensionUnitLength(extensionGuid, nodeId, control);
        }
        catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
        {
            throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
        }
    }

    /// <summary>Reads data from a UVC Extension Unit control via Kernel Streaming.</summary>
    public int GetExtensionUnit(Guid extensionGuid, uint control, Span<byte> data)
    {
        try
        {
            var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
            return GetExtensionUnit(extensionGuid, nodeId, control, data);
        }
        catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
        {
            throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
        }
    }

    /// <summary>Writes data to a UVC Extension Unit control via Kernel Streaming.</summary>
    public void SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data)
    {
        try
        {
            var nodeId = GetCachedExtensionUnitNodeId(extensionGuid, control);
            SetExtensionUnit(extensionGuid, nodeId, control, data);
        }
        catch (Exception ex) when (ex is not ArgumentException and not ObjectDisposedException)
        {
            throw new UsbException($"GetCameraControl failed: {ex.Message}", ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsUvcControls));
    }

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
    /// Finds the topology node ID for a UVC Extension Unit that supports the specified control.
    /// <para />
    /// The mapping is done by trial and error: for each candidate node, we attempt to query the
    /// control length. If the call succeeds we have found the correct node ID. If the call returns
    /// an unexpected error, we continue. This works because each extension unit node will only
    /// accept controls from its own GUID.
    /// <para />
    /// The Windows API does not provide a reliable way to directly query which node corresponds to
    /// a given extension.
    /// </summary>
    private uint? GetExtensionUnitNodeId(Guid extensionGuid, uint control)
    {
        foreach (var nodeId in GetExtensionUnitNodeIds())
        {
            try
            {
                _ = GetExtensionUnitLength(extensionGuid, nodeId, control);
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
    /// <returns>
    /// A list of all the node <see cref="DeviceSpecificNode"/> node IDs.
    /// </returns>
    /// <exception cref="COMException">
    /// The device returned an error from <c>IKsTopologyInfo</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The device does not support <c>IKsTopologyInfo</c>.
    /// </exception>
    private List<uint> GetExtensionUnitNodeIds()
    {
        var xuNodes = new List<uint>();
        lock (_disposeLock)
        {
            ThrowIfDisposed();

            var hr = TopologyInfo.get_NumNodes(out var numNodes);
            Marshal.ThrowExceptionForHR(hr);

            for (uint nodeId = 0; nodeId < numNodes; nodeId++)
            {
                hr = TopologyInfo.get_NodeType(nodeId, out var nodeType);
                Marshal.ThrowExceptionForHR(hr);

                if (nodeType == DeviceSpecificNode)
                {
                    xuNodes.Add(nodeId);
                }
            }
        }
        return xuNodes;
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <returns>The required buffer length in bytes.</returns>
    /// <exception cref="COMException">
    /// The device returned an error (other than ERROR_MORE_DATA).
    /// </exception>
    /// <exception cref="InvalidOperationException">The device does not support IKsControl.</exception>
    private int GetExtensionUnitLength(Guid extensionGuid, uint nodeId, uint control)
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

        lock (_disposeLock)
        {
            ThrowIfDisposed();

            var hr = KsControl.KsProperty(
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
    }

    /// <summary>
    /// Reads data from a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">A buffer to receive the control data.</param>
    /// <returns>The number of bytes actually returned by the device.</returns>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidOperationException">The device does not support IKsControl.</exception>
    private int GetExtensionUnit(Guid extensionGuid, uint nodeId, uint control, Span<byte> data)
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

        lock (_disposeLock)
        {
            ThrowIfDisposed();

            var buffer = new byte[data.Length];
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var hr = KsControl.KsProperty(
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
                pinned.Free();
            }
        }
    }

    /// <summary>
    /// Writes data to a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    /// <param name="extensionGuid">The GUID of the extension unit property set.</param>
    /// <param name="nodeId">The topology node ID of the extension unit.</param>
    /// <param name="control">
    /// The control selector (property ID) within the extension unit.
    /// </param>
    /// <param name="data">The control data to write to the device.</param>
    /// <exception cref="COMException">The device returned an error.</exception>
    /// <exception cref="InvalidOperationException">The device does not support IKsControl.</exception>
    private void SetExtensionUnit(
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

        lock (_disposeLock)
        {
            ThrowIfDisposed();

            var buffer = data.ToArray();
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var hr = KsControl.KsProperty(
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
                pinned.Free();
            }
        }
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_topologyInfo is not null)
                _ = Marshal.ReleaseComObject(_topologyInfo);
            if (_ksControl is not null)
                _ = Marshal.ReleaseComObject(_ksControl);
            if (_videoProcAmp is not null)
                _ = Marshal.ReleaseComObject(_videoProcAmp);
            if (_cameraControl is not null)
                _ = Marshal.ReleaseComObject(_cameraControl);

            _handle.Dispose();
        }
    }
}
