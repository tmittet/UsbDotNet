using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using UsbDotNet.Core;
using UsbDotNet.Descriptor;
using UsbDotNet.LibUsbNative;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.Tests.UsbInterface;

public sealed class Given_a_claimed_USB_interface : IDisposable
{
    private const int ReaderCount = 8;
    private const int Iterations = 20;

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly UsbDotNet.Usb _usb = new(A.Fake<ILibUsb>());

    public void Dispose() => _usb.Dispose();

    [Fact]
    public void Dispose_does_not_throw_when_BulkReads_race_an_in_progress_dispose()
    {
        // Deterministic repro of the race behind the failure in
        // Disposing_the_USB_interface_cancels_an_ongoing_Huddly_device_transfer:
        // BulkRead threads enter while Dispose holds the dispose write lock, and
        // Dispose then tears the lock down. Dispose must neither throw
        // SynchronizationLockException nor deadlock with the racing readers, and
        // every racing BulkRead must report UsbResult.Interrupted.
        var handle = A.Fake<ISafeDeviceHandle>();
        using var device = new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor { DeviceKey = "FAKE_0000_0_0" },
            A.Fake<IUsbConfigDescriptor>()
        );
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        for (var iteration = 1; iteration <= Iterations; iteration++)
        {
            var claimedInterface = A.Fake<ISafeDeviceInterface>();
            A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(claimedInterface);
            var usbInterface = device.ClaimInterface(descriptor);

            using var writeLockHeld = new ManualResetEventSlim();
            using var readersFinished = new CountdownEvent(ReaderCount);
            var allReadersFinishedWhileDisposing = false;
            // The claimed interface is disposed on the disposing thread while
            // UsbInterface.Dispose() holds the write lock. Release the readers here and
            // keep holding the write lock until every reader's BulkRead has returned,
            // proving that all readers observed the in-progress dispose.
            A.CallTo(() => claimedInterface.Dispose())
                .Invokes(() =>
                {
                    writeLockHeld.Set();
                    allReadersFinishedWhileDisposing = readersFinished.Wait(WaitTimeout);
                });

            var readResults = new UsbResult[ReaderCount];
            var readerThreads = Enumerable
                .Range(0, ReaderCount)
                .Select(reader =>
                {
                    var thread = new Thread(() =>
                    {
                        if (!writeLockHeld.Wait(WaitTimeout))
                        {
                            readResults[reader] = UsbResult.Timeout;
                            return;
                        }
                        var buffer = new byte[16];
                        readResults[reader] = usbInterface.BulkRead(
                            buffer,
                            out _,
                            Timeout.Infinite
                        );
                        readersFinished.Signal();
                    })
                    {
                        IsBackground = true,
                    };
                    return thread;
                })
                .ToArray();
            foreach (var readerThread in readerThreads)
            {
                readerThread.Start();
            }

            var dispose = () => usbInterface.Dispose();
            dispose.Should().NotThrow($"dispose failed on iteration {iteration}");
            allReadersFinishedWhileDisposing
                .Should()
                .BeTrue("all BulkRead threads should have completed during the dispose");
            foreach (var readerThread in readerThreads)
            {
                readerThread.Join(WaitTimeout).Should().BeTrue("readers should not hang");
            }
            readResults.Should().AllBeEquivalentTo(UsbResult.Interrupted);
        }
    }

    [Fact]
    public async Task Device_dispose_does_not_deadlock_with_a_concurrent_interface_dispose()
    {
        // Deterministic repro of a lock-order inversion: UsbDevice.Dispose() must not
        // hold its interface lock while calling UsbInterface.Dispose(), because that
        // dispose holds its own dispose monitor while calling back into
        // UsbDevice.ReleaseInterface(), which takes the interface lock.
        var handle = A.Fake<ISafeDeviceHandle>();
#pragma warning disable CA2000 // Dispose objects before losing scope; disposed by the deviceDispose task below
        var device = new UsbDotNet.UsbDevice(
            NullLoggerFactory.Instance,
            _usb,
            A.Fake<ISafeContext>(),
            handle,
            new UsbDeviceDescriptor { DeviceKey = "FAKE_0000_0_0" },
            A.Fake<IUsbConfigDescriptor>()
        );
#pragma warning restore CA2000
        var claimedInterface = A.Fake<ISafeDeviceInterface>();
        A.CallTo(() => handle.ClaimInterface(A<byte>._)).Returns(claimedInterface);

        var inputEndpoint = A.Fake<IUsbEndpointDescriptor>();
        A.CallTo(() => inputEndpoint.EndpointAddress).Returns(new UsbEndpointAddress(0x81));

        // BulkRead resolves its endpoint lazily inside the dispose read lock. Park the
        // reader there, so the interface dispose stays in EnterWriteLock, holding its
        // dispose monitor, until the reader is released.
        using var readerInsideDisposeLock = new ManualResetEventSlim();
        using var releaseReader = new ManualResetEventSlim();
        var descriptor = A.Fake<IUsbInterfaceDescriptor>();
        A.CallTo(() => descriptor.Endpoints)
            .ReturnsLazily(() =>
            {
                readerInsideDisposeLock.Set();
                releaseReader.Wait(WaitTimeout);
                return [inputEndpoint];
            });
        var usbInterface = device.ClaimInterface(descriptor);

        var readResult = UsbResult.UnknownError;
        var readerThread = new Thread(() =>
        {
            var buffer = new byte[16];
            readResult = usbInterface.BulkRead(buffer, out _, Timeout.Infinite);
        })
        {
            IsBackground = true,
        };
        readerThread.Start();
        readerInsideDisposeLock
            .Wait(WaitTimeout)
            .Should()
            .BeTrue("the reader should be parked inside the dispose read lock");

        var interfaceDispose = Task.Run(usbInterface.Dispose);
        // Give the interface dispose time to take its dispose monitor and begin
        // waiting for the parked reader to drain, before the device dispose starts
        await Task.Delay(50);
        var deviceDispose = Task.Run(device.Dispose);
        // Give the device dispose time to reach the interface disposal, then unpark
        // the reader so the interface dispose proceeds into ReleaseInterface()
        await Task.Delay(50);
        releaseReader.Set();

        // On deadlock the dispose tasks never complete
        await Task.WhenAll(interfaceDispose, deviceDispose).WaitAsync(WaitTimeout);
        readerThread.Join(WaitTimeout).Should().BeTrue("the reader should not hang");
        readResult.Should().Be(UsbResult.Interrupted);
    }
}
