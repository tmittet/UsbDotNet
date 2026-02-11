using System.Runtime.InteropServices;
using UsbDotNet.Core;
using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.Extensions;

namespace UsbDotNet.LibUsbNative.Tests.Extensions;

public class libusb_error_ExtensionTest
{
    [Fact]
    public void GetMessage_implements_all_values_defined_in_libusb_error()
    {
        using (new AssertionScope())
        {
            foreach (var value in Enum.GetValues<libusb_error>())
            {
                value.GetMessage().Should().NotBeNullOrEmpty();
                value
                    .GetMessage()
                    .Should()
                    .NotContain(libusb_error_Extension.UnknownLibUsbErrorMessagePrefix);
            }
        }
    }

    [Fact]
    public void GetMessage_values_have_the_expected_format()
    {
        // According to libusb docs:
        // The messages always start with a capital letter and end without any dot.
        using (new AssertionScope())
        {
            foreach (var value in Enum.GetValues<libusb_error>())
            {
                value.GetMessage().Should().NotBeNullOrEmpty();
                value.GetMessage().FirstOrDefault().Should().BeInRange('A', 'Z');
                value.GetMessage().Should().NotMatchRegex(@"[.\s]\z");
            }
        }
    }

    [Fact]
    public void GetMessage_values_equal_the_values_in_the_native_libusb_implementation()
    {
        var api = (ILibUsbApi)new PInvokeLibUsbApi();
        using (new AssertionScope())
        {
            foreach (var value in Enum.GetValues<libusb_error>())
            {
                var ptr = api.libusb_strerror(value);
                value.GetMessage().Should().Be(Marshal.PtrToStringAnsi(ptr));
            }
        }
    }

    [Fact]
    public void ToUsbResult_successfully_maps_all_defined_libusb_error_enum_value_to_UsbResult()
    {
        foreach (var libUsbError in Enum.GetValues<libusb_error>())
        {
            var usbResult = libUsbError.ToUsbResult();
            usbResult.Should().NotBe(UsbResult.UnknownError);
        }
    }

    [Theory]
    [InlineData(1337)]
    [InlineData(123456789)]
    [InlineData(0x7FFFFFFF)]
    public void ToUsbResult_successfully_maps_undefined_libusb_error_enum_value_to_UsbResult(
        int undefinedError
    )
    {
        var libUsbError = (libusb_error)undefinedError;
        var usbResult = libUsbError.ToUsbResult();
        usbResult.Should().Be(UsbResult.UnknownError);
    }
}
