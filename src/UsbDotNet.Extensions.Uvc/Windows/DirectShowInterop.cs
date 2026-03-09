// COM interface definitions for DirectShow device enumeration on Windows.

using System.Runtime.InteropServices;

namespace UsbDotNet.Extensions.Uvc.Windows;

/// <summary>
/// COM ICreateDevEnum — creates a class enumerator for a specified device category.
/// </summary>
[ComImport]
[Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ICreateDevEnum
{
    [PreserveSig]
    int CreateClassEnumerator(
        ref Guid clsidDeviceClass,
        out IEnumMoniker ppEnumMoniker,
        int dwFlags
    );
}

/// <summary>
/// COM IEnumMoniker — enumerates COM monikers.
/// </summary>
[ComImport]
[Guid("00000102-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumMoniker
{
    [PreserveSig]
    int Next(int celt, out IMoniker rgelt, IntPtr pceltFetched);

    [PreserveSig]
    int Skip(int celt);

    void Reset();

    void Clone(out IEnumMoniker ppEnum);
}

/// <summary>
/// COM IMoniker — includes inherited IPersist and IPersistStream vtable slots.
/// Only BindToObject and BindToStorage are called; earlier slots are vtable placeholders
/// required to maintain correct vtable layout for COM interop.
/// </summary>
[ComImport]
[Guid("0000000f-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMoniker
{
    // IPersist
    void GetClassID(out Guid pClassID);

    // IPersistStream
    [PreserveSig]
    int IsDirty();

    void Load(IntPtr pStm);

    void Save(IntPtr pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);

    void GetSizeMax(out long pcbSize);

    // IMoniker
    void BindToObject(
        IntPtr pbc,
        IMoniker pmkToLeft,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppvResult
    );

    void BindToStorage(
        IntPtr pbc,
        IMoniker pmkToLeft,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppvResult
    );
}

/// <summary>
/// COM IPropertyBag — reads named properties from a device moniker.
/// </summary>
[ComImport]
[Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyBag
{
    [PreserveSig]
    int Read(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
        [Out, MarshalAs(UnmanagedType.Struct)] out object pVar,
        IntPtr pErrorLog
    );

    [PreserveSig]
    int Write(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
        [In, MarshalAs(UnmanagedType.Struct)] ref object pVar
    );
}
