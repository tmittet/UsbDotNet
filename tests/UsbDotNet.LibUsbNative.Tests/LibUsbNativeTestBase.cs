using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Tests;

public class LibUsbNativeTestBase(ITestOutputHelper _output, ILibUsbApi _api)
{
    private static readonly ReaderWriterLockSlim rw_lock = new();

    private readonly LibUsb _libUsb = new(_api);

    protected ITestOutputHelper Output { get; } = _output;
    protected List<string> LibUsbOutput { get; } = [];

    protected ISafeContext GetContext(
        libusb_log_level logLevel = libusb_log_level.LIBUSB_LOG_LEVEL_INFO
    )
    {
        var version = _libUsb.GetVersion();
        Output.WriteLine(version.ToString());

        var context = _libUsb.CreateContext();
        RegisterLogCallback(context, logLevel);
        return context;
    }

    private void RegisterLogCallback(ISafeContext context, libusb_log_level logLevel)
    {
        if (logLevel is libusb_log_level.LIBUSB_LOG_LEVEL_NONE)
        {
            return;
        }
        context.RegisterLogCallback(
            (level, message) =>
            {
                Output.WriteLine($"[Libusb][{level}] {message}");
                LibUsbOutput.Add(message);
            }
        );
        try
        {
            context.SetOption(logLevel);
        }
        catch (LibUsbException ex)
            when (OperatingSystem.IsMacOS() && ex.Error is libusb_error.LIBUSB_ERROR_INVALID_PARAM)
        {
            // At this point I'm not sure why this fails on macOS arm64. At first glance, there is
            // nothing in the LibUsb doc or source code indicating that this should not work.
            // Could be a bug in libusb on macOS arm64 or an interop issue? We need to investigate.
            Output.WriteLine(
                $"WARNING: SetOption failed. "
                    + $"Option '{libusb_option.LIBUSB_OPTION_LOG_LEVEL}' not supported on macOS arm64."
            );
        }
    }

    protected static void EnterReadLock(Action action)
    {
        rw_lock.EnterReadLock();
        try
        {
            action();
        }
        finally
        {
            rw_lock.ExitReadLock();
        }
    }

    protected static void EnterWriteLock(Action action)
    {
        rw_lock.EnterReadLock();
        try
        {
            action();
        }
        finally
        {
            rw_lock.ExitReadLock();
        }
    }
}
