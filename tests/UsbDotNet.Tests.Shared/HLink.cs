using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using UsbDotNet.Core;

namespace UsbDotNet.Tests.Shared;

/// <summary>
/// Minimal HLink v0 protocol helper for interacting with Huddly USB devices.
/// </summary>
internal sealed class HLink(IUsbInterface usb, ILogger<HLink>? logger = null)
{
    private const int HeaderSize = 16;
    private const int SaluteReadTimeout = 1000;
    private const int BulkReadTimeout = 500;
    private const int BulkWriteTimeout = 500;

    private byte[] _readBuffer = [];

    /// <summary>
    /// Performs the HLink handshake: reset, flush, salute, and verify response.
    /// </summary>
    /// <exception cref="IOException">
    /// The salute read failed or returned an unexpected response.
    /// </exception>
    internal void Handshake(
        int readTimeout = SaluteReadTimeout,
        int writeTimeout = BulkWriteTimeout
    )
    {
        // Reset (two empty writes, as per protocol)
        var resetResult1 = usb.BulkWrite([], 0, out _, writeTimeout);
        var resetResult2 = usb.BulkWrite([], 0, out _, writeTimeout);

        if (resetResult1 != UsbResult.Success || resetResult2 != UsbResult.Success)
            throw new IOException($"HLink reset write failed: {resetResult1}, {resetResult2}.");

        // Flush any stale data from previous sessions
        _readBuffer = [];
        var flushBuf = new byte[32 * 1024];
        while (usb.BulkRead(flushBuf, out var n, readTimeout) == UsbResult.Success && n > 0)
            logger?.LogDebug("Flushed {Bytes} bytes of stale HLink data.", n);

        // Send salute (single 0x00 byte)
        var saluteWriteResult = usb.BulkWrite([0x00], 1, out _, writeTimeout);
        if (saluteWriteResult != UsbResult.Success)
            throw new IOException($"HLink salute write failed: {saluteWriteResult}.");

        // Expect "HLink v0" response
        var response = new byte[8];
        var saluteReadResult = usb.BulkRead(response, out var bytesRead, readTimeout);
        if (saluteReadResult != UsbResult.Success)
            throw new IOException($"HLink salute read failed: {saluteReadResult}.");
        var saluteResponse = Encoding.UTF8.GetString(response, 0, bytesRead);
        if (saluteResponse != "HLink v0")
            throw new IOException($"Unexpected HLink salute response '{saluteResponse}'.");
    }

    /// <summary>
    /// Sends an HLink request and returns the raw response payload.
    /// Subscribes to reply topic, sends request, receives response and unsubscribes.
    /// </summary>
    /// <exception cref="IOException">A USB transfer failed.</exception>
    /// <exception cref="TimeoutException">No reply was received within the responseTimeout.</exception>
    internal byte[] Query(
        string name,
        int responseTimeout = 1000,
        int sendTimeout = BulkWriteTimeout,
        int readTimeout = BulkReadTimeout
    )
    {
        var replyTopic = $"{name}_reply";
        var topicBytes = Encoding.UTF8.GetBytes(replyTopic);
        Send("hlink-mb-subscribe", topicBytes, sendTimeout);
        Send(name, [], sendTimeout);
        try
        {
            return Receive(replyTopic, responseTimeout, readTimeout);
        }
        finally
        {
            Send("hlink-mb-unsubscribe", topicBytes, sendTimeout);
        }
    }

    public void Send(string msgName, byte[] payload, int timeout = BulkWriteTimeout)
    {
        var nameBytes = Encoding.UTF8.GetBytes(msgName);
        var packet = new byte[HeaderSize + nameBytes.Length + payload.Length];
        // Header layout (little-endian):
        // [0..3] ReqId=0  [4..7] ResId=0  [8..9] Flags=0  [10..11] MsgNameSize  [12..15] PayloadSize
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), (ushort)nameBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), (uint)payload.Length);
        nameBytes.CopyTo(packet, HeaderSize);
        payload.CopyTo(packet, HeaderSize + nameBytes.Length);
        var result = usb.BulkWrite(packet, packet.Length, out _, timeout);
        if (result == UsbResult.Timeout)
            throw new TimeoutException($"HLink send '{msgName}' timed out after {timeout} ms.");
        if (result != UsbResult.Success)
            throw new IOException($"HLink send '{msgName}' failed: {result}.");
    }

    /// <summary>
    /// Reads HLink messages from USB until one matching the given topic arrives or the responseTimeout
    /// is reached. Leftover data is retained in the read buffer for subsequent calls.
    /// </summary>
    public byte[] Receive(
        string topic,
        int timeout = BulkReadTimeout,
        int readTimeout = BulkReadTimeout
    )
    {
        var readBuf = new byte[32 * 1024];
        var deadline = Stopwatch.StartNew();

        while (deadline.ElapsedMilliseconds < timeout)
        {
            // Try to find a matching message in already-buffered data, before doing a new read
            if (TryParseMessage(topic, out var payload))
                return payload;

            // USB bulk reads return variable length chunks. A single HLink message may
            // arrive across multiple reads, or one read may contain multiple messages.
            var result = usb.BulkRead(readBuf, out var bytesRead, readTimeout);
            if (result == UsbResult.Timeout)
            {
                logger?.LogDebug(
                    "HLink receive timed out after {Elapsed} ms, {Buffered} bytes still buffered",
                    deadline.ElapsedMilliseconds,
                    _readBuffer.Length
                );
                continue;
            }
            if (result != UsbResult.Success)
                throw new IOException($"HLink receive failed: {result}.");

            // Accumulate received bytes; we may need several reads before a complete
            // HLink message is available.
            var combined = new byte[_readBuffer.Length + bytesRead];
            _readBuffer.CopyTo(combined, 0);
            Buffer.BlockCopy(readBuf, 0, combined, _readBuffer.Length, bytesRead);
            _readBuffer = combined;
        }

        throw new TimeoutException($"HLink reply '{topic}' not received within {timeout} ms.");
    }

    /// <summary>
    /// Tries to find and extract a message matching the given topic from the read buffer.
    /// Skips non-matching messages and retains any remaining data in the buffer.
    /// </summary>
    private bool TryParseMessage(string topic, out byte[] payload)
    {
        // Walk through buffered data, parsing a complete HLink message:
        //
        // [Header 16 bytes][MsgName (UTF-8)][Payload]
        //
        // Header:
        // Offset  Size  Field
        // 0       4     ReqId
        // 4       4     ResId
        // 8       2     Flags
        // 10      2     MsgNameSize
        // 12      4     PayloadSize
        var pos = 0;
        while (pos + HeaderSize <= _readBuffer.Length)
        {
            // Read lengths from the header to determine the full message size
            var nameLenSpan = _readBuffer.AsSpan(pos + 10, 2);
            var nameLen = (int)BinaryPrimitives.ReadUInt16LittleEndian(nameLenSpan);
            var payloadLenSpan = _readBuffer.AsSpan(pos + 12, 4);
            var payloadLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(payloadLenSpan);
            var msgLen = HeaderSize + nameLen + payloadLen;

            // Not enough data for the complete message; wait for more
            if (pos + msgLen > _readBuffer.Length)
                break;

            // Decode the message name that follows the header
            var name = Encoding.UTF8.GetString(_readBuffer, pos + HeaderSize, nameLen);
            if (name == topic)
            {
                payload = _readBuffer.AsSpan(pos + HeaderSize + nameLen, payloadLen).ToArray();
                // Retain any data after this message for subsequent calls
                _readBuffer = _readBuffer.AsSpan(pos + msgLen).ToArray();
                return true;
            }

            // Not the topic we're waiting for; skip to the next message
            logger?.LogDebug(
                "Discarding HLink message '{Name}' ({Size} bytes), waiting for '{Topic}'",
                name,
                msgLen,
                topic
            );
            pos += msgLen;
        }

        // Discard fully parsed non-matching messages, keep any incomplete data
        if (pos > 0)
            _readBuffer = _readBuffer.AsSpan(pos).ToArray();

        payload = [];
        return false;
    }
}
