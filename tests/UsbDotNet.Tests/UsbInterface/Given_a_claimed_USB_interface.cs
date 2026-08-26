using System.Diagnostics;
using System.Reflection;
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
    public void Dispose_does_not_throw_when_BulkReads_are_waiting_on_the_dispose_lock()
    {
        // Deterministic repro of the race behind the failure in
        // Disposing_the_USB_interface_cancels_an_ongoing_Huddly_device_transfer:
        // BulkRead threads queue on the dispose lock while Dispose holds the
        // write lock, and Dispose then tears the lock down with readers still
        // waiting on it, throwing SynchronizationLockException.
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
            var disposeLock = GetDisposeLock(usbInterface);

            using var writeLockHeld = new ManualResetEventSlim();
            var allReadersWereWaiting = false;
            // The claimed interface is disposed on the disposing thread while
            // UsbInterface.Dispose() holds the write lock. Release the readers here and keep
            // holding the write lock until every reader is queued on it.
            A.CallTo(() => claimedInterface.Dispose())
                .Invokes(() =>
                {
                    writeLockHeld.Set();
                    allReadersWereWaiting = WaitForWaitingReaders(disposeLock, ReaderCount);
                });

            var readResults = new UsbResult[ReaderCount];
            var readerThreads = Enumerable
                .Range(0, ReaderCount)
                .Select(reader => new Thread(() =>
                {
                    writeLockHeld.Wait(WaitTimeout);
                    var buffer = new byte[16];
                    readResults[reader] = usbInterface.BulkRead(buffer, out _, Timeout.Infinite);
                }))
                .ToArray();
            foreach (var readerThread in readerThreads)
            {
                readerThread.Start();
            }

            var dispose = () => usbInterface.Dispose();
            dispose.Should().NotThrow($"dispose failed on iteration {iteration}");
            allReadersWereWaiting
                .Should()
                .BeTrue("all BulkRead threads should have been waiting on the dispose lock");
            foreach (var readerThread in readerThreads)
            {
                readerThread.Join(WaitTimeout).Should().BeTrue("readers should not hang");
            }
            readResults.Should().AllBeEquivalentTo(UsbResult.Interrupted);
        }
    }

    private static ReaderWriterLockSlim GetDisposeLock(IUsbInterface usbInterface)
    {
        // The dispose lock is an implementation detail, but this
        // test must observe it to make the race deterministic.
        var field = typeof(UsbDotNet.UsbInterface).GetField(
            "_disposeLock",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        field.Should().NotBeNull("UsbInterface is expected to have a '_disposeLock' field");
        return (ReaderWriterLockSlim)field!.GetValue(usbInterface)!;
    }

    private static bool WaitForWaitingReaders(ReaderWriterLockSlim disposeLock, int readerCount)
    {
        var stopwatch = Stopwatch.StartNew();
        var spinWait = new SpinWait();
        while (disposeLock.WaitingReadCount < readerCount)
        {
            if (stopwatch.Elapsed > WaitTimeout)
            {
                return false;
            }
            spinWait.SpinOnce();
        }
        return true;
    }
}
