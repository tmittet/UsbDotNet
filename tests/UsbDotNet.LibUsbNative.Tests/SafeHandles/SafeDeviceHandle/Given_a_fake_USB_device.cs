using UsbDotNet.LibUsbNative.Enums;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.SafeDeviceHandle;

public sealed class Given_a_fake_USB_device : IDisposable
{
    private const byte ManufacturerIndex = 1;
    private const byte ProductIndex = 2;
    private const byte SerialNumberIndex = 3;

    private readonly FakeLibusbApi _api = new();
    private readonly ISafeContext _context;
    private readonly ISafeDeviceList _list;
    private readonly ISafeDeviceHandle _handle;

    public Given_a_fake_USB_device()
    {
        _context = new LibUsb(_api).CreateContext();
        _list = _context.GetDeviceList();
        _handle = _list[0].Open();
    }

    [Fact]
    public void GetStringDescriptor_returns_the_string()
    {
        _handle.GetStringDescriptor(ManufacturerIndex).Should().Be("Acme Inc.");
        _handle.GetStringDescriptor(SerialNumberIndex).Should().Be("SN123456");
    }

    [Theory]
    [InlineData("Blåbærsyltetøy")]
    [InlineData("カメラ")]
    [InlineData("Emoji \U0001F3A5 camera")]
    public void GetStringDescriptor_keeps_characters_that_are_not_ASCII(string product)
    {
        _api.ProductUtf16 = FakeLibusbApi.MakeUtf16String(product);

        _handle.GetStringDescriptor(ProductIndex).Should().Be(product);
    }

    [Fact]
    public void GetStringDescriptor_returns_a_string_of_the_maximum_length()
    {
        // A string descriptor is at most 255 bytes: a 2 byte header and 126 UTF-16 characters
        var product = new string('x', 125) + "å";
        _api.ProductUtf16 = FakeLibusbApi.MakeUtf16String(product);
        _api.ProductUtf16.Should().HaveCount(254);

        _handle.GetStringDescriptor(ProductIndex).Should().Be(product);
    }

    [Theory]
    // An odd length; the trailing byte is not a character
    [InlineData(new byte[] { 5, 3, (byte)'A', 0, (byte)'B' }, "A")]
    // More bytes received than the length of the descriptor; the excess is not part of the string
    [InlineData(new byte[] { 4, 3, (byte)'A', 0, (byte)'B', 0 }, "A")]
    public void GetStringDescriptor_returns_the_characters_within_the_descriptor_length(
        byte[] descriptor,
        string expected
    )
    {
        _api.ProductUtf16 = descriptor;

        _handle.GetStringDescriptor(ProductIndex).Should().Be(expected);
    }

    [Fact]
    public void GetStringDescriptor_returns_an_empty_string_for_a_descriptor_without_characters()
    {
        _api.ProductUtf16 = FakeLibusbApi.MakeUtf16String(string.Empty);

        _handle.GetStringDescriptor(ProductIndex).Should().BeEmpty();
    }

    [Fact]
    public void GetStringDescriptor_requests_the_string_in_the_first_language_of_the_device()
    {
        // Two language IDs: German (0x0407), then English (0x0409)
        _api.LangIdx0 = [6, 3, 0x07, 0x04, 0x09, 0x04];

        _ = _handle.GetStringDescriptor(ProductIndex);

        // The language IDs are requested with language 0, then the string with the first of them
        _api.ControlTransferLanguageIds.Should().Equal(0, 0x0407);
    }

    [Fact]
    public void TryGetStringDescriptor_fails_for_index_zero_without_a_request_to_the_device()
    {
        var success = _handle.TryGetStringDescriptor(0, out var value, out var error);

        success.Should().BeFalse();
        value.Should().BeNull();
        error.Should().Be(libusb_error.LIBUSB_ERROR_INVALID_PARAM);
        _api.ControlTransferLanguageIds.Should().BeEmpty();
    }

    [Fact]
    public void TryGetStringDescriptor_returns_the_error_of_a_failed_request()
    {
        _api.InjectError(nameof(_api.libusb_control_transfer), libusb_error.LIBUSB_ERROR_NO_DEVICE);

        var success = _handle.TryGetStringDescriptor(ProductIndex, out var value, out var error);

        success.Should().BeFalse();
        value.Should().BeNull();
        error.Should().Be(libusb_error.LIBUSB_ERROR_NO_DEVICE);
    }

    [Fact]
    public void TryGetStringDescriptor_fails_for_a_string_the_device_does_not_have()
    {
        var success = _handle.TryGetStringDescriptor(index: 9, out _, out var error);

        success.Should().BeFalse();
        error.Should().Be(libusb_error.LIBUSB_ERROR_PIPE);
    }

    [Theory]
    // No language ID
    [InlineData(new byte[] { 2, 3 })]
    // Not a string descriptor
    [InlineData(new byte[] { 4, 2, 0x09, 0x04 })]
    public void TryGetStringDescriptor_fails_for_an_invalid_language_ID_descriptor(
        byte[] languageIds
    )
    {
        _api.LangIdx0 = languageIds;

        var success = _handle.TryGetStringDescriptor(ProductIndex, out _, out var error);

        success.Should().BeFalse();
        error.Should().Be(libusb_error.LIBUSB_ERROR_IO);
    }

    [Theory]
    // Shorter than a descriptor header
    [InlineData(new byte[] { 1 })]
    // A length beyond what was received
    [InlineData(new byte[] { 8, 3, (byte)'A', 0 })]
    // A length shorter than a descriptor header
    [InlineData(new byte[] { 1, 3, (byte)'A', 0 })]
    [InlineData(new byte[] { 0, 3, (byte)'A', 0 })]
    // Not a string descriptor
    [InlineData(new byte[] { 4, 2, (byte)'A', 0 })]
    public void TryGetStringDescriptor_fails_for_an_invalid_string_descriptor(byte[] descriptor)
    {
        _api.ProductUtf16 = descriptor;

        var success = _handle.TryGetStringDescriptor(ProductIndex, out _, out var error);

        success.Should().BeFalse();
        error.Should().Be(libusb_error.LIBUSB_ERROR_IO);
    }

    [Fact]
    public void GetStringDescriptor_throws_when_the_request_fails()
    {
        _api.InjectError(nameof(_api.libusb_control_transfer), libusb_error.LIBUSB_ERROR_TIMEOUT);

        var act = () => _handle.GetStringDescriptor(ProductIndex);

        act.Should()
            .Throw<LibUsbException>()
            .Which.Error.Should()
            .Be(libusb_error.LIBUSB_ERROR_TIMEOUT);
    }

    public void Dispose()
    {
        _handle.Dispose();
        _list.Dispose();
        _context.Dispose();
    }
}
