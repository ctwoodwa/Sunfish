using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class FrameProtocolTests
{
    [Fact]
    public async Task FrameProtocol_RoundTrip_AllMessageTypes()
    {
        var (a, b) = MemoryDuplexPair.Create();
        await using var writer = new FrameProtocol(a);
        await using var reader = new FrameProtocol(b);

        foreach (var (type, payload) in CanonicalSamples())
        {
            await writer.WriteFrameAsync(type, payload, CancellationToken.None);
            var (gotType, gotPayload) = await reader.ReadFrameAsync(CancellationToken.None);
            Assert.Equal(type, gotType);
            Assert.Equal(payload, gotPayload);
        }
    }

    [Fact]
    public async Task FrameProtocol_UuidEncoding_BigEndianRfc4122_NotMicrosoftLayout()
    {
        var (a, b) = MemoryDuplexPair.Create();
        await using var writer = new FrameProtocol(a);
        await using var reader = new FrameProtocol(b);

        var msgId = Guid.Parse("01020304-0506-0708-0900-0a0b0c0d0e0f");
        var text = new TextPayload { MessageId = msgId, Message = "ahoy" };

        await writer.WriteAsync<TextPayload>(MessageType.Text, text, CancellationToken.None);
        var (type, raw) = await reader.ReadFrameAsync(CancellationToken.None);
        Assert.Equal(MessageType.Text, type);

        // The serialized payload contains the 16-byte UUID. Verify the leading
        // 4 bytes match RFC 4122 big-endian (01 02 03 04), NOT MS layout
        // (which would emit 04 03 02 01 for the same Guid).
        var indexOfUuidBlock = FindUuidBlock(raw);
        Assert.True(indexOfUuidBlock >= 0, "Expected to find the 16-byte UUID block in the MessagePack payload.");
        Assert.Equal(0x01, raw[indexOfUuidBlock + 0]);
        Assert.Equal(0x02, raw[indexOfUuidBlock + 1]);
        Assert.Equal(0x03, raw[indexOfUuidBlock + 2]);
        Assert.Equal(0x04, raw[indexOfUuidBlock + 3]);

        var decoded = MessagePackSerializer.Deserialize<TextPayload>(raw, CrewCommsResolver.Options);
        Assert.Equal(msgId, decoded.MessageId);
        Assert.Equal("ahoy", decoded.Message);
    }

    [Fact]
    public async Task FrameProtocol_WriteLock_TwoConcurrentSendersDoNotThrow()
    {
        var (a, b) = MemoryDuplexPair.Create();
        await using var writer = new FrameProtocol(a);
        await using var reader = new FrameProtocol(b);

        var p1 = new byte[] { 1, 2, 3 };
        var p2 = new byte[] { 4, 5, 6, 7 };

        var t1 = writer.WriteFrameAsync(MessageType.Text, p1, CancellationToken.None);
        var t2 = writer.WriteFrameAsync(MessageType.Heartbeat, p2, CancellationToken.None);
        await Task.WhenAll(t1, t2);

        var first = await reader.ReadFrameAsync(CancellationToken.None);
        var second = await reader.ReadFrameAsync(CancellationToken.None);
        var got = new HashSet<(byte, int)> { (first.type, first.payload.Length), (second.type, second.payload.Length) };
        Assert.Contains((MessageType.Text, 3), got);
        Assert.Contains((MessageType.Heartbeat, 4), got);
    }

    [Fact]
    public async Task FrameProtocol_OversizedFrame_ThrowsInvalidData()
    {
        var (a, b) = MemoryDuplexPair.Create();
        await using var writer = new FrameProtocol(a);
        await using var reader = new FrameProtocol(b);

        // Hand-write a frame header claiming a 32 MiB payload (> MaxFramePayloadBytes 16 MiB).
        var oversizedHeader = new byte[5];
        oversizedHeader[0] = 0x00;
        oversizedHeader[1] = 0x00;
        oversizedHeader[2] = 0x00;
        oversizedHeader[3] = 0x02; // length = 0x02000000 = 32 MiB
        oversizedHeader[4] = MessageType.Text;
        await a.WriteAsync(oversizedHeader, CancellationToken.None);
        await a.FlushAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => reader.ReadFrameAsync(CancellationToken.None));
    }

    private static IEnumerable<(byte, byte[])> CanonicalSamples()
    {
        yield return (MessageType.Hello, MessagePackSerializer.Serialize(
            new HelloPayload { TenantId = "demo" }, CrewCommsResolver.Options));
        yield return (MessageType.Heartbeat, MessagePackSerializer.Serialize(
            new PresenceHeartbeat { PeerId = "peer-A", TenantId = "demo" }, CrewCommsResolver.Options));
        yield return (MessageType.Invite, MessagePackSerializer.Serialize(
            new InvitePayload { Capabilities = 0x07 }, CrewCommsResolver.Options));
        yield return (MessageType.Accept, MessagePackSerializer.Serialize(
            new AcceptPayload { Capability = 0x01 }, CrewCommsResolver.Options));
        yield return (MessageType.Confirm, MessagePackSerializer.Serialize(
            new ConfirmPayload { TranscriptHash = new byte[32] }, CrewCommsResolver.Options));
        yield return (MessageType.Bye, Array.Empty<byte>());
        yield return (MessageType.Typing, Array.Empty<byte>());
        yield return (MessageType.Delivered, MessagePackSerializer.Serialize(
            new DeliveredPayload { MessageId = Guid.NewGuid() }, CrewCommsResolver.Options));
        yield return (MessageType.MuteState, new byte[] { 0xC3 });
        yield return (MessageType.Reject, Array.Empty<byte>());
        yield return (MessageType.Text, MessagePackSerializer.Serialize(
            new TextPayload { MessageId = Guid.NewGuid(), Message = "hi" }, CrewCommsResolver.Options));
        yield return (MessageType.AudioFrame, new byte[] { 0xAA, 0xBB }); // Phase 3 stub byte payload
    }

    private static int FindUuidBlock(byte[] payload)
    {
        // Locate any 16-byte bin8 region starting with 0x04 (length prefix) 0x10.
        for (var i = 0; i + 17 < payload.Length; i++)
        {
            if (payload[i] == 0xC4 && payload[i + 1] == 0x10) // bin8 with length 16
                return i + 2;
        }
        return -1;
    }
}

internal sealed class MemoryDuplexStream : IDuplexStream
{
    private readonly System.IO.Pipelines.Pipe _hereToPeer;
    private readonly System.IO.Pipelines.Pipe _peerToHere;

    private MemoryDuplexStream(System.IO.Pipelines.Pipe hereToPeer, System.IO.Pipelines.Pipe peerToHere)
    {
        _hereToPeer = hereToPeer;
        _peerToHere = peerToHere;
    }

    public static (MemoryDuplexStream a, MemoryDuplexStream b) CreatePair()
    {
        var leftToRight = new System.IO.Pipelines.Pipe();
        var rightToLeft = new System.IO.Pipelines.Pipe();
        var a = new MemoryDuplexStream(hereToPeer: leftToRight, peerToHere: rightToLeft);
        var b = new MemoryDuplexStream(hereToPeer: rightToLeft, peerToHere: leftToRight);
        return (a, b);
    }

    public Stream Stream => throw new NotSupportedException("Use the typed async members.");

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        var result = await _peerToHere.Reader.ReadAsync(ct);
        if (result.Buffer.IsEmpty && result.IsCompleted) return 0;
        var sequence = result.Buffer;
        var toCopy = (int)Math.Min(buffer.Length, sequence.Length);
        sequence.Slice(0, toCopy).CopyTo(buffer.Span);
        _peerToHere.Reader.AdvanceTo(sequence.GetPosition(toCopy));
        return toCopy;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
    {
        await _hereToPeer.Writer.WriteAsync(buffer, ct);
    }

    public Task FlushAsync(CancellationToken ct)
    {
        var t = _hereToPeer.Writer.FlushAsync(ct);
        return t.IsCompletedSuccessfully ? Task.CompletedTask : t.AsTask();
    }

    public ValueTask DisposeAsync()
    {
        _hereToPeer.Writer.Complete();
        return ValueTask.CompletedTask;
    }
}

internal static class MemoryDuplexPair
{
    public static (MemoryDuplexStream a, MemoryDuplexStream b) Create() => MemoryDuplexStream.CreatePair();
}
