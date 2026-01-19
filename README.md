# UsbDotNet

A modern, cross platform C# binding for [libusb](https://libusb.info/).

## License

UsbDotNet is licensed under the MIT License (see `/LICENSE`). It dynamically links to libusb,
which is licensed under the LGPL-2.1. Precompiled [libusb](https://libusb.info/) binaries are
included in compliance with LGPL.

## Third-Party libraries

### libusb-1.0
- The libusb-1.0 library is bundled with this project for convenience
- libusb-1.0 is part of the [libusb project](https://github.com/libusb/) and is licensed under
  [LGPL-2.1](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html)
- Precompiled binaries are included in `/src/UsbDotNet.LibUsbNative/runtimes/`
- You may replace the binaries with any LGPL-compliant versions

### Licensing Notes
- The full LGPL-2.1 license text is provided in `/LICENSE.libusb`
- Use of libusb doesn’t imply endorsement from the libusb project
- If you modify or redistribute the libusb binaries, you must follow the
  [LGPL-2.1 terms](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html)
