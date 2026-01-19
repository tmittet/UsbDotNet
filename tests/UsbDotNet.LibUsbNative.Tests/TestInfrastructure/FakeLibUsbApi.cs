using System.Runtime.InteropServices;
using System.Text;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Functions;
using UsbDotNet.LibUsbNative.Structs;

namespace UsbDotNet.LibUsbNative.Tests.TestInfrastructure;

/// <summary>
/// Enhanced in-memory fake of libusb for tests.
/// Provides:
/// - Deterministic mocked data for every API call
/// - Realistic unmanaged allocations for device lists & configuration descriptors
/// - Error injection per API (first-in-first-consumed)
/// - Proper unmanaged resource cleanup (IDisposable + finalizer)
/// </summary>
internal sealed class FakeLibusbApi : ILibUsbApi
{
    // ---------------------------
    // Simple in-memory device model
    // ---------------------------
    internal libusb_device_descriptor Device = new(
        bLength: 18,
        bDescriptorType: libusb_descriptor_type.LIBUSB_DT_DEVICE,
        bcdUSB: 0x0200,
        bDeviceClass: libusb_class_code.LIBUSB_CLASS_MISCELLANEOUS,
        bDeviceSubClass: 0x02,
        bDeviceProtocol: 0x01,
        bMaxPacketSize0: 64,
        idVendor: 0x1234,
        idProduct: 0x5678,
        bcdDevice: 0x0100,
        iManufacturer: 1,
        iProduct: 2,
        iSerialNumber: 3,
        bNumConfigurations: 1
    );

    // String descriptors (index 0: LANGID table)
    public byte[] ManufacturerUtf16 = MakeUtf16String("Acme Inc.");
    public byte[] ProductUtf16 = MakeUtf16String("USB Gizmo");
    public byte[] SerialAscii = Encoding.ASCII.GetBytes("SN123456");
    public byte[] LangIdx0 = [4, 3, 0x09, 0x04]; // English (US)

    public static byte[] MakeUtf16String(string s)
    {
        var payload = Encoding.Unicode.GetBytes(s);
        var buf = new byte[payload.Length + 2];
        buf[0] = (byte)buf.Length;
        buf[1] = 3; // USB string descriptor type
        Array.Copy(payload, 0, buf, 2, payload.Length);
        return buf;
    }

    // Device list: single fake device pointer
    private readonly List<IntPtr> _devices = [new IntPtr(0x1000)];

    // Allocations we must free
    private readonly List<IntPtr> _deviceListBlocks = [];
    private readonly List<ConfigAllocation> _configAllocs = [];

    private record struct ConfigAllocation(
        IntPtr Config,
        IntPtr InterfaceArray,
        IntPtr AltSettingArray,
        IntPtr EndpointArray
    );

    // Hotplug
    public libusb_hotplug_callback_fn? LastCb;
    public int LastCbHandle = 42;

    // State tracking
    private readonly Dictionary<libusb_option, IntPtr> _options = [];
    private readonly HashSet<IntPtr> _openHandles = [];
    private readonly HashSet<(IntPtr Handle, byte Interface)> _claimed = [];
    private int _nextHandle = 0x3000;
    private int _nextTransfer = 0x4000;

    // Version / strerror
    private readonly IntPtr _versionPtr;
    private readonly IntPtr _versionRcPtr;
    private readonly IntPtr _versionDescPtr;
    private readonly Dictionary<libusb_error, IntPtr> _strErrorPtrs;

    // Error injection (API name -> queue of factories)
    private readonly Dictionary<string, Queue<Func<libusb_error>>> _errorInjectors = new(
        StringComparer.Ordinal
    );
    private readonly object _lock = new();

    // Disposal
    private bool _disposed;

    public FakeLibusbApi()
    {
        // Allocate version structure + strings once.
        _versionRcPtr = AllocAnsi("mock");
        _versionDescPtr = AllocAnsi("LibUsb Test Fake");

        var verNative = new native_libusb_version
        {
            major = 1,
            minor = 0,
            micro = 0,
            nano = 0,
            rc = _versionRcPtr,
            describe = _versionDescPtr,
        };
        _versionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<native_libusb_version>());
        Marshal.StructureToPtr(verNative, _versionPtr, false);

        _strErrorPtrs = [];
        foreach (var error in Enum.GetValues<libusb_error>())
        {
            _strErrorPtrs[error] = AllocAnsi(error.ToString());
        }
    }

    private static IntPtr AllocAnsi(string s)
    {
        var bytes = Encoding.ASCII.GetBytes(s + "\0");
        var p = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, p, bytes.Length);
        return p;
    }

    // ---------------------------
    // Error Injection API
    // ---------------------------
    public void InjectError(string apiName, libusb_error error) =>
        InjectError(apiName, () => error);

    public void InjectErrors(string apiName, IEnumerable<libusb_error> errors)
    {
        foreach (var e in errors)
            InjectError(apiName, e);
    }

    public void InjectError(string apiName, Func<libusb_error> factory)
    {
        lock (_lock)
        {
            if (!_errorInjectors.TryGetValue(apiName, out var q))
            {
                q = new Queue<Func<libusb_error>>();
                _errorInjectors[apiName] = q;
            }
            q.Enqueue(factory);
        }
    }

    private bool TryConsumeInjected(string name, out libusb_error error)
    {
        lock (_lock)
        {
            if (_errorInjectors.TryGetValue(name, out var q) && q.Count > 0)
            {
                error = q.Dequeue().Invoke();
                return true;
            }
        }
        error = libusb_error.LIBUSB_SUCCESS;
        return false;
    }

    private libusb_error MaybeFail(string apiName) =>
        TryConsumeInjected(apiName, out var e) ? e : libusb_error.LIBUSB_SUCCESS;

    private libusb_error MaybeFail(string apiName, out libusb_error error)
    {
        error = MaybeFail(apiName);
        return error;
    }

#pragma warning disable IDE0060 // Remove unused parameter

    // ------------- Context / Options -------------
    public libusb_error libusb_init(out IntPtr ctx)
    {
        ctx = IntPtr.Zero;
        if (MaybeFail(nameof(libusb_init), out var err) != libusb_error.LIBUSB_SUCCESS)
            return err;
        ctx = new IntPtr(0xDEADBEEF);
        return libusb_error.LIBUSB_SUCCESS;
    }

    public void libusb_exit(IntPtr ctx) { }

    public libusb_error libusb_set_option(IntPtr ctx, libusb_option option, int value)
    {
        if (MaybeFail(nameof(libusb_set_option), out var err) != libusb_error.LIBUSB_SUCCESS)
            return err;
        _options[option] = (IntPtr)value;
        return libusb_error.LIBUSB_SUCCESS;
    }

    public libusb_error libusb_set_option(IntPtr ctx, libusb_option option, IntPtr value)
    {
        if (MaybeFail(nameof(libusb_set_option), out var err) != libusb_error.LIBUSB_SUCCESS)
            return err;
        _options[option] = value;
        return libusb_error.LIBUSB_SUCCESS;
    }

    public libusb_error libusb_handle_events_completed(IntPtr ctx, IntPtr completed) =>
        MaybeFail(nameof(libusb_handle_events_completed));

    public void libusb_interrupt_event_handler(IntPtr ctx) { }

    public IntPtr libusb_get_version() => _versionPtr;

    public int libusb_has_capability(libusb_capability capability) => 1;

    public IntPtr libusb_strerror(libusb_error errorCode) =>
        _strErrorPtrs.TryGetValue(errorCode, out var p)
            ? p
            : _strErrorPtrs[libusb_error.LIBUSB_ERROR_OTHER];

    // ------------- Device list -------------
    public libusb_error libusb_get_device_list(IntPtr ctx, out IntPtr list)
    {
        list = IntPtr.Zero;
        if (MaybeFail(nameof(libusb_get_device_list), out var err) != libusb_error.LIBUSB_SUCCESS)
            return err;

        var count = _devices.Count;
        var total = (count + 1) * IntPtr.Size;
        var block = Marshal.AllocHGlobal(total);
        for (var i = 0; i < count; i++)
            Marshal.WriteIntPtr(block, i * IntPtr.Size, _devices[i]);
        Marshal.WriteIntPtr(block, count * IntPtr.Size, IntPtr.Zero);

        list = block;
        _deviceListBlocks.Add(block);
        return (libusb_error)count;
    }

    public void libusb_free_device_list(IntPtr list, int unrefDevices)
    {
        if (list != IntPtr.Zero && _deviceListBlocks.Remove(list))
            Marshal.FreeHGlobal(list);
    }

    public void libusb_ref_device(IntPtr dev) { }

    public void libusb_unref_device(IntPtr dev) { }

    // ------------- Device metadata -------------
    public libusb_error libusb_get_device_descriptor(IntPtr dev, out libusb_device_descriptor desc)
    {
        desc = default;
        if (
            MaybeFail(nameof(libusb_get_device_descriptor), out var error)
            != libusb_error.LIBUSB_SUCCESS
        )
        {
            return error;
        }
        desc = Device;
        return libusb_error.LIBUSB_SUCCESS;
    }

    public libusb_error libusb_get_active_config_descriptor(IntPtr dev, out IntPtr config) =>
        libusb_get_config_descriptor(dev, 0, out config);

    public libusb_error libusb_get_config_descriptor(IntPtr dev, ushort index, out IntPtr config)
    {
        config = IntPtr.Zero;
        if (
            MaybeFail(nameof(libusb_get_config_descriptor), out var error)
            != libusb_error.LIBUSB_SUCCESS
        )
        {
            return error;
        }
        if (index != 0)
            return libusb_error.LIBUSB_ERROR_NOT_FOUND;

        // 2 endpoints: EP1 OUT bulk, EP1 IN bulk
        var epCount = 2;
        var epSize = Marshal.SizeOf<native_libusb_endpoint_descriptor>();
        var epPtr = Marshal.AllocHGlobal(epCount * epSize);

        void WriteEndpoint(int offset, byte address, byte attrs, ushort maxPacket, byte interval)
        {
            var ep = new native_libusb_endpoint_descriptor
            {
                bLength = 7,
                bDescriptorType = (byte)libusb_descriptor_type.LIBUSB_DT_ENDPOINT,
                bEndpointAddress = address,
                bmAttributes = attrs,
                wMaxPacketSize = maxPacket,
                bInterval = interval,
                bRefresh = 0,
                bSynchAddress = 0,
                extra = IntPtr.Zero,
                extra_length = 0,
            };
            Marshal.StructureToPtr(ep, IntPtr.Add(epPtr, offset * epSize), false);
        }

        WriteEndpoint(0, 0x01, 0x02, 512, 0);
        WriteEndpoint(1, 0x81, 0x02, 512, 0);

        var ifDesc = new native_libusb_interface_descriptor
        {
            bLength = 9,
            bDescriptorType = (byte)libusb_descriptor_type.LIBUSB_DT_INTERFACE,
            bInterfaceNumber = 0,
            bAlternateSetting = 0,
            bNumEndpoints = (byte)epCount,
            bInterfaceClass = (byte)libusb_class_code.LIBUSB_CLASS_MISCELLANEOUS,
            bInterfaceSubClass = 0x02,
            bInterfaceProtocol = 0x01,
            iInterface = 0,
            endpoint = epPtr,
            extra = IntPtr.Zero,
            extra_length = 0,
        };
        var ifDescPtr = Marshal.AllocHGlobal(Marshal.SizeOf<native_libusb_interface_descriptor>());
        Marshal.StructureToPtr(ifDesc, ifDescPtr, false);

        var nativeInterface = new native_libusb_interface
        {
            altsetting = ifDescPtr,
            num_altsetting = 1,
        };
        var interfaceArrayPtr = Marshal.AllocHGlobal(Marshal.SizeOf<native_libusb_interface>());
        Marshal.StructureToPtr(nativeInterface, interfaceArrayPtr, false);

        var totalLength = (ushort)(
            Marshal.SizeOf<native_libusb_config_descriptor>()
            + Marshal.SizeOf<native_libusb_interface>()
            + Marshal.SizeOf<native_libusb_interface_descriptor>()
            + epCount * Marshal.SizeOf<native_libusb_endpoint_descriptor>()
        );

        var cfg = new native_libusb_config_descriptor
        {
            bLength = 9,
            bDescriptorType = (byte)libusb_descriptor_type.LIBUSB_DT_CONFIG,
            wTotalLength = totalLength,
            bNumInterfaces = 1,
            bConfigurationValue = 1,
            iConfiguration = 0,
            bmAttributes = (byte)(
                libusb_config_desc_attributes.RESERVED_MUST_BE_SET
                | libusb_config_desc_attributes.SELF_POWERED
            ),
            MaxPower = 50,
            interfacePtr = interfaceArrayPtr,
            extra = IntPtr.Zero,
            extra_length = 0,
        };
        var cfgPtr = Marshal.AllocHGlobal(Marshal.SizeOf<native_libusb_config_descriptor>());
        Marshal.StructureToPtr(cfg, cfgPtr, false);

        _configAllocs.Add(new ConfigAllocation(cfgPtr, interfaceArrayPtr, ifDescPtr, epPtr));

        config = cfgPtr;
        return libusb_error.LIBUSB_SUCCESS;
    }

    public void libusb_free_config_descriptor(IntPtr config)
    {
        for (var i = 0; i < _configAllocs.Count; i++)
        {
            if (_configAllocs[i].Config == config)
            {
                var alloc = _configAllocs[i];
                Marshal.FreeHGlobal(alloc.EndpointArray);
                Marshal.FreeHGlobal(alloc.AltSettingArray);
                Marshal.FreeHGlobal(alloc.InterfaceArray);
                Marshal.FreeHGlobal(alloc.Config);
                _configAllocs.RemoveAt(i);
                break;
            }
        }
    }

    public byte libusb_get_bus_number(IntPtr dev) => 3;

    public byte libusb_get_device_address(IntPtr dev) => 17;

    public byte libusb_get_port_number(IntPtr dev) => 1;

    // ------------- Open / close -------------
    public libusb_error libusb_open(IntPtr dev, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (MaybeFail(nameof(libusb_open), out var err) != libusb_error.LIBUSB_SUCCESS)
            return err;
        handle = new IntPtr(_nextHandle++);
        _openHandles.Add(handle);
        return libusb_error.LIBUSB_SUCCESS;
    }

    public void libusb_close(IntPtr handle)
    {
        _openHandles.Remove(handle);
        _claimed.RemoveWhere(c => c.Handle == handle);
    }

    // ------------- Config / Interface -------------
    public libusb_error libusb_claim_interface(IntPtr handle, byte interface_number)
    {
        var err = MaybeFail(nameof(libusb_claim_interface));
        if (err == libusb_error.LIBUSB_SUCCESS)
            _claimed.Add((handle, interface_number));
        return err;
    }

    public libusb_error libusb_release_interface(IntPtr handle, byte interface_number)
    {
        var err = MaybeFail(nameof(libusb_release_interface));
        if (err == libusb_error.LIBUSB_SUCCESS)
            _claimed.Remove((handle, interface_number));
        return err;
    }

    // ------------- Strings -------------
    public libusb_error libusb_get_string_descriptor_ascii(
        IntPtr h,
        byte idx,
        byte[] data,
        int length
    )
    {
        if (
            MaybeFail(nameof(libusb_get_string_descriptor_ascii), out var error)
            != libusb_error.LIBUSB_SUCCESS
        )
        {
            return error;
        }
        if (idx == 3)
        {
            var n = Math.Min(length, SerialAscii.Length);
            Array.Copy(SerialAscii, data, n);
            return (libusb_error)n;
        }
        return libusb_error.LIBUSB_ERROR_NOT_FOUND;
    }

    // ------------- Halt / Reset -------------
    public libusb_error libusb_reset_device(IntPtr handle) =>
        MaybeFail(nameof(libusb_reset_device));

    // ------------- Transfers -------------
    public IntPtr libusb_alloc_transfer(int iso_packets) => new(_nextTransfer++);

    public void libusb_free_transfer(IntPtr transfer) { }

    public libusb_error libusb_submit_transfer(IntPtr transfer) =>
        MaybeFail(nameof(libusb_submit_transfer));

    public libusb_error libusb_cancel_transfer(IntPtr transfer) =>
        MaybeFail(nameof(libusb_cancel_transfer));

    // ------------- Hotplug -------------
    public libusb_error libusb_hotplug_register_callback(
        IntPtr ctx,
        libusb_hotplug_event events,
        libusb_hotplug_flag flags,
        int vendorId,
        int productId,
        int devClass,
        libusb_hotplug_callback_fn cb,
        IntPtr user_data,
        out IntPtr callbackHandle
    )
    {
        callbackHandle = 0;
        if (
            MaybeFail(nameof(libusb_hotplug_register_callback), out var error)
            != libusb_error.LIBUSB_SUCCESS
        )
        {
            return error;
        }
        LastCb = cb;
        callbackHandle = LastCbHandle;
        return libusb_error.LIBUSB_SUCCESS;
    }

    public void libusb_hotplug_deregister_callback(IntPtr ctx, IntPtr callbackHandle)
    {
#pragma warning disable CA2020 // Prevent behavioral change
        if ((int)callbackHandle == LastCbHandle)
            LastCb = null;
#pragma warning restore CA2020 // Prevent behavioral change
    }

#pragma warning restore IDE0060 // Remove unused parameter

    ~FakeLibusbApi() => FreeAllResources();

    private void FreeAllResources()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (var block in _deviceListBlocks)
        {
            if (block != IntPtr.Zero)
                Marshal.FreeHGlobal(block);
        }
        _deviceListBlocks.Clear();

        foreach (var cfg in _configAllocs)
        {
            if (cfg.EndpointArray != IntPtr.Zero)
                Marshal.FreeHGlobal(cfg.EndpointArray);
            if (cfg.AltSettingArray != IntPtr.Zero)
                Marshal.FreeHGlobal(cfg.AltSettingArray);
            if (cfg.InterfaceArray != IntPtr.Zero)
                Marshal.FreeHGlobal(cfg.InterfaceArray);
            if (cfg.Config != IntPtr.Zero)
                Marshal.FreeHGlobal(cfg.Config);
        }
        _configAllocs.Clear();

        foreach (var kv in _strErrorPtrs)
        {
            if (kv.Value != IntPtr.Zero)
                Marshal.FreeHGlobal(kv.Value);
        }
        _strErrorPtrs.Clear();

        if (_versionPtr != IntPtr.Zero)
            Marshal.FreeHGlobal(_versionPtr);
        if (_versionRcPtr != IntPtr.Zero)
            Marshal.FreeHGlobal(_versionRcPtr);
        if (_versionDescPtr != IntPtr.Zero)
            Marshal.FreeHGlobal(_versionDescPtr);
    }
}
