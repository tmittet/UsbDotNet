using UsbDotNet.Core;
using UsbDotNet.LibUsbNative.Enums;

namespace UsbDotNet.LibUsbNative.Tests;

public sealed class LibUsbExceptionTest
{
    [Fact]
    public void The_error_and_the_matching_code_are_set_from_the_libusb_error()
    {
        var exception = new LibUsbException(libusb_error.LIBUSB_ERROR_NO_DEVICE, "No device.");

        exception.Error.Should().Be(libusb_error.LIBUSB_ERROR_NO_DEVICE);
        exception.Code.Should().Be(UsbResult.NoDevice);
        exception.Message.Should().Be("No device.");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void The_inner_exception_is_kept_along_with_the_libusb_error()
    {
        var inner = new InvalidOperationException("Inner.");

        var exception = new LibUsbException(libusb_error.LIBUSB_ERROR_BUSY, "Busy.", inner);

        exception.Error.Should().Be(libusb_error.LIBUSB_ERROR_BUSY);
        exception.Code.Should().Be(UsbResult.ResourceBusy);
        exception.Message.Should().Be("Busy.");
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void An_exception_without_a_libusb_error_has_the_error_other()
    {
        var exception = new LibUsbException("Failed.");

        exception.Error.Should().Be(libusb_error.LIBUSB_ERROR_OTHER);
        exception.Code.Should().Be(UsbResult.UnknownError);
    }
}
