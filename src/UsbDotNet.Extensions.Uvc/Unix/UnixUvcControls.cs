using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace UsbDotNet.Extensions.Uvc.Unix;

internal sealed class UnixUvcControls : IUvcControls
{
    private readonly IUsbDevice _device;
    private readonly byte _interfaceNumber;
    private readonly byte? _cameraControlEntityId;
    private readonly byte? _imageSettingEntityId;
    private readonly ConcurrentDictionary<Guid, byte> _extensionUnitEntityIds = new();

    internal UnixUvcControls(IUsbDevice device, byte interfaceNumber)
    {
        _device = device;
        _interfaceNumber = interfaceNumber;
        _cameraControlEntityId = UvcDescriptor.GetCameraControlEntityId(device, interfaceNumber);
        _imageSettingEntityId = UvcDescriptor.GetImageSettingEntityId(device, interfaceNumber);
    }

    public void GetCameraControl(
        UvcCameraControl cameraControl,
        out int value,
        out UvcControl flags
    )
    {
        value = TransferCameraControl(cameraControl, UvcControlRequest.GetCurrent);
        flags = UvcControl.Manual;
    }

    public void SetCameraControl(
        UvcCameraControl cameraControl,
        int value,
        UvcControl flags = UvcControl.Manual
    )
    {
        if (_cameraControlEntityId is null)
        {
            throw new InvalidOperationException(
                $"Camera control {cameraControl} is not supported because no Camera Terminal entity was found in the UVC descriptors."
            );
        }
        var (controlSelector, bufferSize, offset) = UvcTransfer.GetCameraControlDescriptor(
            cameraControl
        );

        // Pan and Tilt share an 8-byte control; do a read-modify-write to preserve the other axis.
        if (bufferSize == 8)
        {
            var buffer = new byte[8];
            var readResult = _device.ControlReadUvc(
                buffer,
                out _,
                UvcControlRequest.GetCurrent,
                _interfaceNumber,
                _cameraControlEntityId.Value,
                controlSelector
            );
            UvcTransfer.ThrowIfFailed(readResult, $"CameraControl {cameraControl} GetCurrent");
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
            var writeResult = _device.ControlWriteUvc(
                buffer,
                out _,
                UvcControlRequest.SetCurrent,
                _interfaceNumber,
                _cameraControlEntityId.Value,
                controlSelector
            );
            UvcTransfer.ThrowIfFailed(writeResult, $"CameraControl {cameraControl} SetCurrent");
        }
        else
        {
            var buffer = new byte[bufferSize];
            UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
            var result = _device.ControlWriteUvc(
                buffer,
                out _,
                UvcControlRequest.SetCurrent,
                _interfaceNumber,
                _cameraControlEntityId.Value,
                controlSelector
            );
            UvcTransfer.ThrowIfFailed(result, $"CameraControl {cameraControl} SetCurrent");
        }
    }

    public void GetCameraControlRange(
        UvcCameraControl cameraControl,
        out int minValue,
        out int maxValue,
        out int stepSize,
        out int defaultValue,
        out UvcControl capsFlags
    )
    {
        minValue = TransferCameraControl(cameraControl, UvcControlRequest.GetMinimum);
        maxValue = TransferCameraControl(cameraControl, UvcControlRequest.GetMaximum);
        stepSize = TransferCameraControl(cameraControl, UvcControlRequest.GetResolution);
        defaultValue = TransferCameraControl(cameraControl, UvcControlRequest.GetDefault);
        capsFlags = UvcControl.Manual;
    }

    // The UVC spec returns the full buffer for pantilt; extract the int at the right offset.
    private int TransferCameraControl(UvcCameraControl cameraControl, UvcControlRequest request)
    {
        if (_cameraControlEntityId is null)
        {
            throw new InvalidOperationException(
                $"Camera control {cameraControl} is not supported because no Camera Terminal entity was found in the UVC descriptors."
            );
        }
        var (controlSelector, bufferSize, offset) = UvcTransfer.GetCameraControlDescriptor(
            cameraControl
        );
        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            _cameraControlEntityId.Value,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(result, $"CameraControl {cameraControl} {request}");
        return UvcTransfer.ReadInt(buffer, offset, bufferSize <= 4 ? bufferSize : 4);
    }

    public void GetImageSetting(UvcImageSetting imageSetting, out int value, out UvcControl flags)
    {
        value = TransferImageSetting(imageSetting, UvcControlRequest.GetCurrent);
        flags = UvcControl.Manual;
    }

    public void SetImageSetting(
        UvcImageSetting imageSetting,
        int value,
        UvcControl flags = UvcControl.Manual
    )
    {
        if (_imageSettingEntityId is null)
        {
            throw new InvalidOperationException(
                $"Image setting {imageSetting} is not supported because no Processing Unit entity was found in the UVC descriptors."
            );
        }
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(imageSetting);
        var buffer = new byte[bufferSize];
        UvcTransfer.WriteInt(buffer, 0, bufferSize, value);
        var result = _device.ControlWriteUvc(
            buffer,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            _imageSettingEntityId.Value,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(result, $"VideoProcAmp {imageSetting} SetCurrent");
    }

    public void GetImageSettingRange(
        UvcImageSetting imageSetting,
        out int min,
        out int max,
        out int step,
        out int defaultValue,
        out UvcControl capsFlags
    )
    {
        min = TransferImageSetting(imageSetting, UvcControlRequest.GetMinimum);
        max = TransferImageSetting(imageSetting, UvcControlRequest.GetMaximum);
        step = TransferImageSetting(imageSetting, UvcControlRequest.GetResolution);
        defaultValue = TransferImageSetting(imageSetting, UvcControlRequest.GetDefault);
        capsFlags = UvcControl.Manual;
    }

    private int TransferImageSetting(UvcImageSetting imageSetting, UvcControlRequest request)
    {
        if (_imageSettingEntityId is null)
        {
            throw new InvalidOperationException(
                $"Image setting {imageSetting} is not supported because no Processing Unit entity was found in the UVC descriptors."
            );
        }
        var (controlSelector, bufferSize) = UvcTransfer.GetImageSettingDescriptor(imageSetting);
        var buffer = new byte[bufferSize];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            request,
            _interfaceNumber,
            _imageSettingEntityId.Value,
            controlSelector
        );
        UvcTransfer.ThrowIfFailed(result, $"VideoProcAmp {imageSetting} {request}");
        return UvcTransfer.ReadInt(buffer, 0, bufferSize);
    }

    private byte GetCachedExtensionUnitEntityId(Guid extensionGuid) =>
        _extensionUnitEntityIds.GetOrAdd(
            extensionGuid,
            guid =>
                UvcDescriptor.GetExtensionUnitEntityId(_device, _interfaceNumber, guid)
                ?? throw new InvalidOperationException(
                    $"Extension unit is not supported because no matching entity ID was found in the UVC descriptors."
                )
        );

    public int GetExtensionUnit(Guid extensionGuid, uint xuControl, Span<byte> data)
    {
        var entityId = GetCachedExtensionUnitEntityId(extensionGuid);
        return GetExtensionUnit(extensionGuid, entityId, xuControl, data);
    }

    public void SetExtensionUnit(Guid extensionGuid, uint xuControl, ReadOnlySpan<byte> data)
    {
        var entityId = GetCachedExtensionUnitEntityId(extensionGuid);
        SetExtensionUnit(extensionGuid, entityId, xuControl, data);
    }

    public int GetExtensionUnitLength(Guid extensionGuid, uint xuControl)
    {
        var entityId = GetCachedExtensionUnitEntityId(extensionGuid);
        return GetExtensionUnitLength(extensionGuid, entityId, xuControl);
    }

    /// <remarks>The extension unit GUID is not used on Linux/macOS.</remarks>
    public int GetExtensionUnit(Guid extensionGuid, uint entityId, uint xuControl, Span<byte> data)
    {
        var result = _device.ControlReadUvc(
            data,
            out var bytesRead,
            UvcControlRequest.GetCurrent,
            _interfaceNumber,
            (byte)entityId,
            (byte)xuControl
        );
        UvcTransfer.ThrowIfFailed(result, $"ExtensionUnit Get cs=0x{xuControl:X2}");
        return bytesRead;
    }

    /// <remarks>The extension unit GUID is not used on Linux/macOS.</remarks>
    public void SetExtensionUnit(
        Guid extensionGuid,
        uint entityId,
        uint xuControl,
        ReadOnlySpan<byte> data
    )
    {
        var result = _device.ControlWriteUvc(
            data,
            out _,
            UvcControlRequest.SetCurrent,
            _interfaceNumber,
            (byte)entityId,
            (byte)xuControl
        );
        UvcTransfer.ThrowIfFailed(result, $"ExtensionUnit Set cs=0x{xuControl:X2}");
    }

    /// <remarks>
    /// Issues a UVC GET_LEN request. Returns the 16-bit length value from the 2-byte response.
    /// The extension unit GUID is not used on Linux/macOS.
    /// </remarks>
    public int GetExtensionUnitLength(Guid extensionGuid, uint entityId, uint xuControl)
    {
        Span<byte> buffer = stackalloc byte[2];
        var result = _device.ControlReadUvc(
            buffer,
            out _,
            UvcControlRequest.GetLength,
            _interfaceNumber,
            (byte)entityId,
            (byte)xuControl
        );
        UvcTransfer.ThrowIfFailed(result, $"ExtensionUnit GetLength cs=0x{xuControl:X2}");
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    // Device is not owned; no-op dispose.
    public void Dispose() { }
}
