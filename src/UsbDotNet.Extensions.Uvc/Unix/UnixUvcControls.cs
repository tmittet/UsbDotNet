using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace UsbDotNet.Extensions.Uvc.Unix;

internal sealed class UnixUvcControls : IUvcControls
{
    private readonly IUsbDevice _device;
    private readonly byte _interfaceNumber;
    private readonly ConcurrentDictionary<Guid, byte> _extensionUnitEntityIds = new();
    private byte? _cameraControlEntityId,
        _imageSettingEntityId;

    internal UnixUvcControls(IUsbDevice device, byte interfaceNumber)
    {
        _device = device;
        _interfaceNumber = interfaceNumber;
    }

    /// <inheritdoc />
    public int GetCameraControl(UvcCameraControl cameraControl, out UvcControlType controlType)
    {
        controlType = UvcControlType.Manual;
        return ReadCameraControl(cameraControl, UvcControlRequest.GetCurrent);
    }

    /// <inheritdoc />
    public void SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        var cameraControlEntityId = GetCameraControlEntityIdOrThrow();
        var (controlSelector, bufferSize, offset) = UvcTransfer.GetCameraControlDescriptor(
            cameraControl
        );

        if (cameraControl is not UvcCameraControl.Pan and not UvcCameraControl.Tilt)
        {
            var buffer = new byte[bufferSize];
            UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
            var result = _device.ControlWriteUvc(
                buffer,
                out _,
                UvcControlRequest.SetCurrent,
                _interfaceNumber,
                cameraControlEntityId,
                controlSelector
            );
            UvcTransfer.ThrowIfFailed(
                result,
                $"ControlWriteUvc(request=SetCurrent, control={cameraControl})"
            );
            return;
        }

        // Pan and Tilt share an 8-byte control; do a read-modify-write to preserve the other axis
        if (bufferSize != 8)
        {
            throw new InvalidOperationException(
                $"Unexpected buffer size {bufferSize} for {cameraControl} control; expected 8."
            );
        }
        var ptBuffer = new byte[8];
        var readResult = _device.ControlReadUvc(
            ptBuffer,
            out _,
            UvcControlRequest.GetCurrent,
            _interfaceNumber,
            cameraControlEntityId,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(
            readResult,
            $"ControlReadUvc(request=GetCurrent, control={cameraControl})"
        );
        BinaryPrimitives.WriteInt32LittleEndian(ptBuffer.AsSpan(offset, 4), value);
        var writeResult = _device.ControlWriteUvc(
            ptBuffer,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            cameraControlEntityId,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(
            writeResult,
            $"ControlWriteUvc(request=SetCurrent, control={cameraControl})"
        );
    }

    /// <inheritdoc />
    public void GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControlType capsFlags
    )
    {
        minValue = ReadCameraControl(cameraControl, UvcControlRequest.GetMinimum);
        maxValue = ReadCameraControl(cameraControl, UvcControlRequest.GetMaximum);
        stepSize = ReadCameraControl(cameraControl, UvcControlRequest.GetResolution);
        defaultValue = ReadCameraControl(cameraControl, UvcControlRequest.GetDefault);
        capsFlags = UvcControlType.Manual;
    }

    private int ReadCameraControl(UvcCameraControl cameraControl, UvcControlRequest request)
    {
        var cameraControlEntityId = GetCameraControlEntityIdOrThrow();
        var (controlSelector, bufferSize, offset) = UvcTransfer.GetCameraControlDescriptor(
            cameraControl
        );
        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            cameraControlEntityId,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlReadUvc(request={request}, control={cameraControl})"
        );
        // The UVC spec returns the full buffer for pan & tilt; extract the int at the right offset
        return UvcTransfer.ReadInt(buffer, offset, bufferSize <= 4 ? bufferSize : 4);
    }

    /// <inheritdoc />
    public int GetImageSetting(UvcImageSetting imageSetting, out UvcControlType controlType)
    {
        controlType = UvcControlType.Manual;
        return ReadImageSetting(imageSetting, UvcControlRequest.GetCurrent);
    }

    /// <inheritdoc />
    public void SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControlType controlType = UvcControlType.Manual
    )
    {
        var imageSettingEntityId = GetImageSettingEntityIdOrThrow();
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(imageSetting);
        var buffer = new byte[bufferSize];
        UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
        var result = _device.ControlWriteUvc(
            buffer,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            imageSettingEntityId,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlWriteUvc(request=SetCurrent, setting={imageSetting})"
        );
    }

    /// <inheritdoc />
    public void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int min,
        out int max,
        out int step,
        out int defaultValue,
        out UvcControlType controlType
    )
    {
        min = ReadImageSetting(imageSetting, UvcControlRequest.GetMinimum);
        max = ReadImageSetting(imageSetting, UvcControlRequest.GetMaximum);
        step = ReadImageSetting(imageSetting, UvcControlRequest.GetResolution);
        defaultValue = ReadImageSetting(imageSetting, UvcControlRequest.GetDefault);
        controlType = UvcControlType.Manual;
    }

    private int ReadImageSetting(UvcImageSetting imageSetting, UvcControlRequest request)
    {
        var imageSettingEntityId = GetImageSettingEntityIdOrThrow();
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(imageSetting);
        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            imageSettingEntityId,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlReadUvc(request={request}, control={imageSetting})"
        );
        return UvcTransfer.ReadInt(buffer, 0, bufferSize);
    }

    /// <inheritdoc />
    public int GetExtensionUnit(Guid extensionGuid, uint control, Span<byte> data)
    {
        var entityId = GetExtensionUnitEntityId(extensionGuid);
        return GetExtensionUnit(extensionGuid, entityId, control, data);
    }

    /// <inheritdoc />
    public void SetExtensionUnit(Guid extensionGuid, uint control, ReadOnlySpan<byte> data)
    {
        var entityId = GetExtensionUnitEntityId(extensionGuid);
        SetExtensionUnit(extensionGuid, entityId, control, data);
    }

    /// <inheritdoc />
    public int GetExtensionUnitLength(Guid extensionGuid, uint control)
    {
        var entityId = GetExtensionUnitEntityId(extensionGuid);
        return GetExtensionUnitLength(extensionGuid, entityId, control);
    }

    /// <inheritdoc />
    public int GetExtensionUnit(Guid extensionGuid, uint entityId, uint control, Span<byte> data)
    {
        var result = _device.ControlReadUvc(
            data,
            out var bytesRead,
            UvcControlRequest.GetCurrent,
            _interfaceNumber,
            (byte)entityId,
            (byte)control
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlReadUvc(request=GetCurrent, entityId=0x{entityId:X2} control=0x{control:X2})"
        );
        return bytesRead;
    }

    /// <inheritdoc />
    public void SetExtensionUnit(
        Guid extensionGuid,
        uint entityId,
        uint control,
        ReadOnlySpan<byte> data
    )
    {
        var result = _device.ControlWriteUvc(
            data,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            (byte)entityId,
            (byte)control
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlWriteUvc(request=SetCurrent, entityId=0x{entityId:X2} control=0x{control:X2})"
        );
    }

    /// <inheritdoc />
    public int GetExtensionUnitLength(Guid extensionGuid, uint entityId, uint control)
    {
        Span<byte> buffer = stackalloc byte[2];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            UvcControlRequest.GetLength,
            _interfaceNumber,
            (byte)entityId,
            (byte)control
        );
        UvcTransfer.ThrowIfFailed(
            result,
            $"ControlReadUvc(request=GetCurrent, entityId=0x{entityId:X2} control=0x{control:X2})"
        );
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    private byte GetCameraControlEntityIdOrThrow() =>
        _cameraControlEntityId ??=
            UvcDescriptor.GetCameraControlEntityId(_device, _interfaceNumber)
            ?? throw new InvalidOperationException(
                "Camera control request is not supported; "
                    + "no camera terminal entity was found in the UVC descriptors."
            );

    private byte GetImageSettingEntityIdOrThrow() =>
        _imageSettingEntityId ??=
            UvcDescriptor.GetImageSettingEntityId(_device, _interfaceNumber)
            ?? throw new InvalidOperationException(
                "Image setting request is not supported; "
                    + "no processing unit entity was found in the UVC descriptors."
            );

    private byte GetExtensionUnitEntityId(Guid extensionGuid) =>
        _extensionUnitEntityIds.GetOrAdd(
            extensionGuid,
            guid =>
                UvcDescriptor.GetExtensionUnitEntityId(_device, _interfaceNumber, guid)
                ?? throw new InvalidOperationException(
                    $"Extension unit request is not supported; "
                        + $"no entity ID matching '{guid}' was found in the UVC descriptors."
                )
        );

    // Device is not owned; no-op dispose.
    public void Dispose() { }
}
