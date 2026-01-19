#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace UsbDotNet.LibUsbNative.Enums;

#if NET8_0_OR_GREATER
[JsonConverter(typeof(JsonStringEnumConverter<libusb_endpoint_number>))]
#endif
public enum libusb_endpoint_number : byte
{
    EP_00 = 0,
    EP_01 = 1,
    EP_02 = 2,
    EP_03 = 3,
    EP_04 = 4,
    EP_05 = 5,
    EP_06 = 6,
    EP_07 = 7,
    EP_08 = 8,
    EP_09 = 9,
    EP_10 = 10,
    EP_11 = 11,
    EP_12 = 12,
    EP_13 = 13,
    EP_14 = 14,
    EP_15 = 15,
}
