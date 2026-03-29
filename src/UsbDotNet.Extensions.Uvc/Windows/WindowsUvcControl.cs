using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// Windows UVC control implementation using DirectShow and Kernel Streaming COM interfaces.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsUvcControl : IUvcControl
{
    private const int ErrorInsufficientBuffer = unchecked((int)0x8007007A);
    private const int ErrorMoreData = unchecked((int)0x800700EA);
    public static readonly Guid DeviceSpecificNode = new("941C7AC0-C559-11D0-8A2B-00A0C9255AC1");

    private readonly SafeVideoDeviceHandle _handle;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, uint> _extensionUnitNodeIds = new();

    private readonly object _directShowObject;
    private readonly IAMCameraControl? _cameraControl;
    private readonly IAMVideoProcAmp? _videoProcAmp;
    private readonly IKsControl? _ksControl;
    private readonly IKsTopologyInfo? _topologyInfo;

    private readonly object _disposeLock = new();
    private bool _disposed;

    internal WindowsUvcControl(SafeVideoDeviceHandle handle, ILogger<WindowsUvcControl> logger)
    {
        _handle = handle;
        _logger = logger;

        var unknownDevice =
            handle.IsInvalid || handle.IsClosed
                ? throw new ObjectDisposedException(nameof(SafeVideoDeviceHandle))
                : handle.DangerousGetHandle();

        _directShowObject = Marshal.GetObjectForIUnknown(unknownDevice);
        _cameraControl = _directShowObject as IAMCameraControl;
        _videoProcAmp = _directShowObject as IAMVideoProcAmp;
        _ksControl = _directShowObject as IKsControl;
        _topologyInfo = _directShowObject as IKsTopologyInfo;
    }

    /// <summary>
    /// Gets the current value and control type of a camera terminal control property.
    /// </summary>
    public UsbResult GetCameraControl(
        UvcCameraControl cameraControl,
        out int value,
        out UvcControlType controlType
    )
    {
        value = 0;
        controlType = default;
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_cameraControl is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _cameraControl.Get((int)cameraControl, out value, out var rawFlags);
            if (hr == 0)
            {
                controlType = (UvcControlType)rawFlags;
                return UsbResult.Success;
            }
            return MapHResult(hr);
        }
    }

    /// <summary>
    /// Sets the value of a camera terminal control property.
    /// </summary>
    public UsbResult SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_cameraControl is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _cameraControl.Set((int)cameraControl, value, (int)controlType);
            return hr == 0 ? UsbResult.Success : MapHResult(hr);
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a camera control property.
    /// </summary>
    public UsbResult GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType controlType
    )
    {
        minValue = maxValue = stepSize = defaultValue = 0;
        controlType = default;
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_cameraControl is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _cameraControl.GetRange(
                (int)cameraControl,
                out minValue,
                out maxValue,
                out stepSize,
                out defaultValue,
                out var rawFlags
            );
            if (hr == 0)
            {
                controlType = (UvcControlType)rawFlags;
                return UsbResult.Success;
            }
            return MapHResult(hr);
        }
    }

    /// <summary>
    /// Gets the current value and control type of a video processing amplifier property.
    /// </summary>
    public UsbResult GetImageSetting(
        UvcImageSetting imageSetting,
        out int value,
        out UvcControlType controlType
    )
    {
        value = 0;
        controlType = default;
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_videoProcAmp is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _videoProcAmp.Get((int)imageSetting, out value, out var rawFlags);
            if (hr == 0)
            {
                controlType = (UvcControlType)rawFlags;
                return UsbResult.Success;
            }
            return MapHResult(hr);
        }
    }

    /// <summary>
    /// Sets the value of a video processing amplifier property.
    /// </summary>
    public UsbResult SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_videoProcAmp is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _videoProcAmp.Set((int)imageSetting, value, (int)controlType);
            return hr == 0 ? UsbResult.Success : MapHResult(hr);
        }
    }

    /// <summary>
    /// Gets the supported range (minValue, maxValue, stepSize, default)
    /// and capabilities for a video proc amp property.
    /// </summary>
    public UsbResult GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    )
    {
        minValue = maxValue = stepSize = defaultValue = 0;
        capsFlags = default;
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_videoProcAmp is null)
            {
                return UsbResult.NotSupported;
            }
            var hr = _videoProcAmp.GetRange(
                (int)imageSetting,
                out minValue,
                out maxValue,
                out stepSize,
                out defaultValue,
                out var rawFlags
            );
            if (hr == 0)
            {
                capsFlags = (UvcControlType)rawFlags;
                return UsbResult.Success;
            }
            return MapHResult(hr);
        }
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    public UsbResult GetExtensionUnitLength(Guid extensionGuid, uint control, out int length)
    {
        length = 0;
        var result = GetCachedExtensionUnitNodeId(extensionGuid, control, out var nodeId);
        return result == UsbResult.Success
            ? GetExtensionUnitLength(extensionGuid, nodeId, control, out length)
            : result;
    }

    /// <summary>Reads data from a UVC Extension Unit control via Kernel Streaming.</summary>
    public UsbResult GetExtensionUnit(
        Guid extensionGuid,
        uint control,
        Span<byte> data,
        out int bytesRead
    )
    {
        bytesRead = 0;
        var result = GetCachedExtensionUnitNodeId(extensionGuid, control, out var nodeId);
        return result == UsbResult.Success
            ? GetExtensionUnit(extensionGuid, nodeId, control, data, out bytesRead)
            : result;
    }

    /// <summary>Writes data to a UVC Extension Unit control via Kernel Streaming.</summary>
    public UsbResult SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data)
    {
        var result = GetCachedExtensionUnitNodeId(extensionGuid, control, out var nodeId);
        return result == UsbResult.Success
            ? SetExtensionUnit(extensionGuid, nodeId, control, data)
            : result;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsUvcControl));
    }

    private UsbResult GetCachedExtensionUnitNodeId(
        Guid extensionGuid,
        uint control,
        out uint nodeId
    )
    {
        if (_extensionUnitNodeIds.TryGetValue(extensionGuid, out nodeId))
            return UsbResult.Success;

        var result = GetExtensionUnitNodeId(extensionGuid, control, out nodeId);
        if (result == UsbResult.Success)
            nodeId = _extensionUnitNodeIds.GetOrAdd(extensionGuid, nodeId);
        return result;
    }

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
    private UsbResult GetExtensionUnitNodeId(Guid extensionGuid, uint control, out uint nodeId)
    {
        nodeId = 0;
        var result = GetExtensionUnitNodeIds(out var candidates);
        if (result != UsbResult.Success)
            return result;

        foreach (var candidate in candidates)
        {
            if (
                GetExtensionUnitLength(extensionGuid, candidate, control, out _)
                == UsbResult.Success
            )
            {
                nodeId = candidate;
                return UsbResult.Success;
            }
        }
        return UsbResult.NotSupported;
    }

    /// <summary>
    /// Finds all Kernel Streaming topology node IDs for a UVC Extension Unit.
    /// </summary>
    /// <remarks>
    /// Uses <c>IKsTopologyInfo</c> to enumerate all nodes in the DirectShow filter.
    /// </remarks>
    private UsbResult GetExtensionUnitNodeIds(out List<uint> xuNodes)
    {
        xuNodes = new List<uint>();
        lock (_disposeLock)
        {
            ThrowIfDisposed();
            if (_topologyInfo is null)
                return UsbResult.NotSupported;

            var hr = _topologyInfo.get_NumNodes(out var numNodes);
            if (hr != 0)
                return MapHResult(hr);

            for (uint nodeId = 0; nodeId < numNodes; nodeId++)
            {
                hr = _topologyInfo.get_NodeType(nodeId, out var nodeType);
                if (hr != 0)
                    return MapHResult(hr);

                if (nodeType == DeviceSpecificNode)
                {
                    xuNodes.Add(nodeId);
                }
            }
        }
        return UsbResult.Success;
    }

    /// <summary>
    /// Queries the data length for a UVC Extension Unit control via Kernel Streaming.
    /// Sends a get request with a zero-length buffer; the driver returns the required size.
    /// </summary>
    private UsbResult GetExtensionUnitLength(
        Guid extensionGuid,
        uint nodeId,
        uint control,
        out int length
    )
    {
        length = 0;
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
            if (_ksControl is null)
                return UsbResult.NotSupported;

            var hr = _ksControl.KsProperty(
                ref node,
                Marshal.SizeOf<KspNode>(),
                IntPtr.Zero,
                0,
                out var bytesReturned
            );

            // ErrorMoreData (0x800700EA) and ErrorInsufficientBuffer (0x8007007A) expected
            if (hr is 0 or ErrorMoreData or ErrorInsufficientBuffer)
            {
                length = bytesReturned;
                return UsbResult.Success;
            }
            return MapHResult(hr);
        }
    }

    /// <summary>
    /// Reads data from a UVC Extension Unit control via Kernel Streaming.
    /// </summary>
    private UsbResult GetExtensionUnit(
        Guid extensionGuid,
        uint nodeId,
        uint control,
        Span<byte> data,
        out int bytesRead
    )
    {
        bytesRead = 0;
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
            if (_ksControl is null)
                return UsbResult.NotSupported;

            var buffer = new byte[data.Length];
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var hr = _ksControl.KsProperty(
                    ref node,
                    Marshal.SizeOf<KspNode>(),
                    pinned.AddrOfPinnedObject(),
                    buffer.Length,
                    out var returned
                );
                if (hr == 0)
                {
                    buffer.AsSpan(0, returned).CopyTo(data);
                    bytesRead = returned;
                    return UsbResult.Success;
                }
                return MapHResult(hr);
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
    private UsbResult SetExtensionUnit(
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
            if (_ksControl is null)
                return UsbResult.NotSupported;

            var buffer = data.ToArray();
            var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var hr = _ksControl.KsProperty(
                    ref node,
                    Marshal.SizeOf<KspNode>(),
                    pinned.AddrOfPinnedObject(),
                    buffer.Length,
                    out _
                );
                return hr == 0 ? UsbResult.Success : MapHResult(hr);
            }
            finally
            {
                pinned.Free();
            }
        }
    }

    private UsbResult MapHResult(int hResult, [CallerMemberName] string caller = "")
    {
        // Standard COM errors
        const int eNotImpl = unchecked((int)0x80004001);
        const int eNoInterface = unchecked((int)0x80004002);
        const int ePointer = unchecked((int)0x80004003);
        const int eAbort = unchecked((int)0x80004004);
        const int eFail = unchecked((int)0x80004005);

        // HRESULT_FROM_WIN32 errors
        const int eAccessDenied = unchecked((int)0x80070005);
        const int eOutOfMemory = unchecked((int)0x8007000E);
        const int eNotReady = unchecked((int)0x80070015);
        const int eGenFailure = unchecked((int)0x8007001F);
        const int eInvalidArg = unchecked((int)0x80070057);
        const int eBrokenPipe = unchecked((int)0x8007006D);
        const int eSemTimeout = unchecked((int)0x80070079);
        const int eTimeout = unchecked((int)0x80070102);
        const int eBusy = unchecked((int)0x800700AA);
        const int eDeviceNotConnected = unchecked((int)0x8007048F);
        const int eNotFound = unchecked((int)0x80070490);
        const int eSetNotFound = unchecked((int)0x80070492);

        if (hResult == 0)
            return UsbResult.Success;

        var result = hResult switch
        {
            eNotImpl or eNoInterface or eSetNotFound => UsbResult.NotSupported,
            eInvalidArg or ePointer => UsbResult.InvalidParameter,
            eAccessDenied => UsbResult.AccessDenied,
            eNotReady or eDeviceNotConnected => UsbResult.NoDevice,
            eNotFound => UsbResult.NotFound,
            eBusy => UsbResult.ResourceBusy,
            eSemTimeout or eTimeout => UsbResult.Timeout,
            eBrokenPipe => UsbResult.PipeError,
            eAbort => UsbResult.Interrupted,
            eOutOfMemory => UsbResult.InsufficientMemory,
            eFail or eGenFailure => UsbResult.IoError,
            _ => UsbResult.OtherError,
        };
        _logger.LogDebug(
            "{Caller} failed. {UsbResult} (0x{HResult:X8}): {Message}",
            caller,
            result,
            hResult,
            Marshal.GetExceptionForHR(hResult)?.Message
        );
        return result;
    }

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            _disposed = true;

            _ = Marshal.ReleaseComObject(_directShowObject);
            _handle.Dispose();
        }
    }
}
