using System.Buffers.Binary;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UsbDotNet.Core;

namespace UsbDotNet.Extensions.Uvc.Unix;

internal sealed class UnixUvcControl : IUvcControl
{
    private readonly IUsbDevice _device;
    private readonly byte _interfaceNumber;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, byte> _extensionUnitEntityIds = new();
    private byte? _cameraControlEntityId,
        _imageSettingEntityId;

    internal UnixUvcControl(IUsbDevice device, byte interfaceNumber, ILogger logger)
    {
        _device = device;
        _interfaceNumber = interfaceNumber;
        _logger = logger;
    }

    /// <inheritdoc />
    public UsbResult GetCameraControl(
        UvcCameraControl cameraControl,
        out int value,
        out UvcControlType controlType
    )
    {
        controlType = UvcControlType.Manual;
        return ReadCameraControl(cameraControl, UvcControlRequest.GetCurrent, out value);
    }

    /// <inheritdoc />
    public UsbResult SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        if (!TryGetCameraControlEntityId(out var cameraControlEntityId))
            return UsbResult.NotSupported;

        var (control, bufferSize, offset) = cameraControl.GetCameraControlDescriptor();

        if (cameraControl is not UvcCameraControl.Pan and not UvcCameraControl.Tilt)
        {
            var buffer = new byte[bufferSize];
            UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
            return _device.ControlWriteUvc(
                buffer,
                out _,
                UvcControlRequest.SetCurrent,
                _interfaceNumber,
                cameraControlEntityId,
                control
            );
        }

        // Pan and Tilt share an 8-byte control; do a read-modify-write to preserve the other axis
        var ptBuffer = new byte[8];
        var readResult = _device.ControlReadUvc(
            ptBuffer,
            out _,
            UvcControlRequest.GetCurrent,
            _interfaceNumber,
            cameraControlEntityId,
            control
        );
        if (readResult != UsbResult.Success)
            return readResult;

        BinaryPrimitives.WriteInt32LittleEndian(ptBuffer.AsSpan(offset, 4), value);
        return _device.ControlWriteUvc(
            ptBuffer,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            cameraControlEntityId,
            control
        );
    }

    /// <inheritdoc />
    public UsbResult GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    )
    {
        capsFlags = UvcControlType.Manual;

        var result = ReadCameraControl(cameraControl, UvcControlRequest.GetMinimum, out minValue);
        if (result != UsbResult.Success)
        {
            maxValue = stepSize = defaultValue = 0;
            return result;
        }

        result = ReadCameraControl(cameraControl, UvcControlRequest.GetMaximum, out maxValue);
        if (result != UsbResult.Success)
        {
            stepSize = defaultValue = 0;
            return result;
        }

        result = ReadCameraControl(cameraControl, UvcControlRequest.GetResolution, out stepSize);
        if (result != UsbResult.Success)
        {
            defaultValue = 0;
            return result;
        }

        result = ReadCameraControl(cameraControl, UvcControlRequest.GetDefault, out defaultValue);
        return result;
    }

    private UsbResult ReadCameraControl(
        UvcCameraControl cameraControl,
        UvcControlRequest request,
        out int value
    )
    {
        value = 0;
        if (!TryGetCameraControlEntityId(out var cameraControlEntityId))
            return UsbResult.NotSupported;

        var (control, bufferSize, offset) = cameraControl.GetCameraControlDescriptor();
        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            cameraControlEntityId,
            control
        );
        if (result != UsbResult.Success)
            return result;

        // The UVC spec returns the full buffer for pan & tilt; extract the int at the right offset
        value = UvcTransfer.ReadInt(buffer, offset, bufferSize <= 4 ? bufferSize : 4);
        return UsbResult.Success;
    }

    /// <inheritdoc />
    public UsbResult GetImageSetting(
        UvcImageSetting imageSetting,
        out int value,
        out UvcControlType controlType
    )
    {
        controlType = UvcControlType.Manual;
        return ReadImageSetting(imageSetting, UvcControlRequest.GetCurrent, out value);
    }

    /// <inheritdoc />
    public UsbResult SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        if (!TryGetImageSettingEntityId(out var imageSettingEntityId))
            return UsbResult.NotSupported;

        if (!imageSetting.TryGetImageSettingDescriptor(out var controlId, out var bufferSize))
            return UsbResult.NotSupported;

        var buffer = new byte[bufferSize];
        UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
        return _device.ControlWriteUvc(
            buffer,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            imageSettingEntityId,
            controlId
        );
    }

    /// <inheritdoc />
    public UsbResult GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int min,
        out int max,
        out int step,
        out int defaultValue,
        out UvcControlType controlType
    )
    {
        controlType = UvcControlType.Manual;

        var result = ReadImageSetting(imageSetting, UvcControlRequest.GetMinimum, out min);
        if (result != UsbResult.Success)
        {
            max = step = defaultValue = 0;
            return result;
        }

        result = ReadImageSetting(imageSetting, UvcControlRequest.GetMaximum, out max);
        if (result != UsbResult.Success)
        {
            step = defaultValue = 0;
            return result;
        }

        result = ReadImageSetting(imageSetting, UvcControlRequest.GetResolution, out step);
        if (result != UsbResult.Success)
        {
            defaultValue = 0;
            return result;
        }

        result = ReadImageSetting(imageSetting, UvcControlRequest.GetDefault, out defaultValue);
        return result;
    }

    private UsbResult ReadImageSetting(
        UvcImageSetting imageSetting,
        UvcControlRequest request,
        out int value
    )
    {
        value = 0;
        if (!TryGetImageSettingEntityId(out var imageSettingEntityId))
            return UsbResult.NotSupported;

        if (!imageSetting.TryGetImageSettingDescriptor(out var controlId, out var bufferSize))
            return UsbResult.NotSupported;

        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            imageSettingEntityId,
            controlId
        );
        if (result != UsbResult.Success)
        {
            return result;
        }

        value = UvcTransfer.ReadInt(buffer, 0, bufferSize);
        return UsbResult.Success;
    }

    /// <inheritdoc />
    public UsbResult GetExtensionUnitLength(Guid extensionGuid, uint control, out int length)
    {
        length = 0;
        if (!TryGetExtensionUnitEntityId(extensionGuid, out var entityId))
            return UsbResult.NotSupported;

        Span<byte> buffer = stackalloc byte[2];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            UvcControlRequest.GetLength,
            _interfaceNumber,
            entityId,
            (byte)control
        );
        if (result != UsbResult.Success)
        {
            return result;
        }

        length = BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        return UsbResult.Success;
    }

    /// <inheritdoc />
    public UsbResult GetExtensionUnit(
        Guid extensionGuid,
        uint control,
        Span<byte> data,
        out int bytesRead
    )
    {
        bytesRead = 0;
        if (!TryGetExtensionUnitEntityId(extensionGuid, out var entityId))
            return UsbResult.NotSupported;

        var result = _device.ControlReadUvc(
            data,
            out var read,
            UvcControlRequest.GetCurrent,
            _interfaceNumber,
            entityId,
            (byte)control
        );
        if (result != UsbResult.Success)
        {
            return result;
        }

        bytesRead = read;
        return UsbResult.Success;
    }

    /// <inheritdoc />
    public UsbResult SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data) =>
        TryGetExtensionUnitEntityId(extensionGuid, out var entityId)
            ? _device.ControlWriteUvc(
                data,
                out _,
                UvcControlRequest.SetCurrent,
                _interfaceNumber,
                entityId,
                (byte)control
            )
            : UsbResult.NotSupported;

    private bool TryGetCameraControlEntityId(out byte entityId)
    {
        _cameraControlEntityId ??= _device.GetUvcCameraControlEntityId(_interfaceNumber);
        if (_cameraControlEntityId is { } id)
        {
            entityId = id;
            return true;
        }
        entityId = 0;
        return false;
    }

    private bool TryGetImageSettingEntityId(out byte entityId)
    {
        _imageSettingEntityId ??= _device.GetUvcImageSettingEntityId(_interfaceNumber);
        if (_imageSettingEntityId is { } id)
        {
            entityId = id;
            return true;
        }
        entityId = 0;
        return false;
    }

    private bool TryGetExtensionUnitEntityId(Guid extensionGuid, out byte entityId)
    {
        // Can't use GetOrAdd with a factory that might fail, so check first
        if (_extensionUnitEntityIds.TryGetValue(extensionGuid, out entityId))
            return true;

        var resolved = _device.GetUvcExtensionUnitEntityId(_interfaceNumber, extensionGuid);
        if (resolved is byte id)
        {
            entityId = _extensionUnitEntityIds.GetOrAdd(extensionGuid, id);
            return true;
        }
        entityId = 0;
        return false;
    }

    // Device is not owned; no-op dispose.
    public void Dispose() { }
}
