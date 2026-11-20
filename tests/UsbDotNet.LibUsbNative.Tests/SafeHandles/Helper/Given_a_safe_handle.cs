using System.Runtime.InteropServices;
using UsbDotNet.LibUsbNative.SafeHandles;

namespace UsbDotNet.LibUsbNative.Tests.SafeHandles.Helper;

public class Given_a_safe_handle
{
    [Fact]
    public void A_held_reference_defers_the_release_of_a_disposed_handle()
    {
        var handle = new CountingSafeHandle();

        using (SafeHelper.AcquireReference(handle))
        {
            // Another thread disposing the handle during a native call
            handle.Dispose();

            // A SafeHandle is not closed until the last reference is released
            handle.IsClosed.Should().BeFalse();
            handle.ReleaseCount.Should().Be(0);
        }

        handle.IsClosed.Should().BeTrue();
        handle.ReleaseCount.Should().Be(1);
    }

    [Fact]
    public void A_released_reference_leaves_the_handle_open()
    {
        using var handle = new CountingSafeHandle();

        using (SafeHelper.AcquireReference(handle)) { }

        handle.IsClosed.Should().BeFalse();
        handle.ReleaseCount.Should().Be(0);
    }

    [Fact]
    public void AcquireReference_throws_when_the_handle_is_disposed()
    {
        var handle = new CountingSafeHandle();
        handle.Dispose();

        var act = () =>
        {
            using var reference = SafeHelper.AcquireReference(handle);
        };

        act.Should().Throw<ObjectDisposedException>();
        handle.ReleaseCount.Should().Be(1);
    }

    private sealed class CountingSafeHandle : SafeHandle
    {
        public int ReleaseCount { get; private set; }

        public CountingSafeHandle()
            : base(IntPtr.Zero, ownsHandle: true)
        {
            handle = new IntPtr(1);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            ReleaseCount++;
            return true;
        }
    }
}
