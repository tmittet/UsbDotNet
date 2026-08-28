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
}
