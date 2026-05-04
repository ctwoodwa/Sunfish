using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Sunfish.Foundation.Transport;

namespace Sunfish.Blocks.CrewComms.Protocol;

/// <summary>
/// Length-prefix wire framing for crew-comms protocol traffic over a single
/// <see cref="IDuplexStream"/>. One frame == 4-byte little-endian length
/// prefix || 1-byte type discriminator || MessagePack-encoded payload.
/// </summary>
/// <remarks>
/// <para>
/// All writes are serialized behind a <see cref="SemaphoreSlim"/> gate per
/// the cohort precedent in <c>kernel-sync/Protocol/WebSocketSyncDaemonTransport.cs</c>:
/// the underlying transport (e.g. <c>ClientWebSocket</c>) tolerates exactly
/// one concurrent send, so multiple producers (HEARTBEAT timer + outbound
/// TEXT + signaling INVITE/ACCEPT/CONFIRM) MUST be queued through a single
/// gate or the transport throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Phase 1 ships a simple <see cref="SemaphoreSlim"/>; Phase 3 will upgrade
/// to a priority-aware <c>Channel</c> producer/consumer pattern so 50 fps
/// audio cannot block control frames. Do NOT pre-implement that here.
/// </para>
/// </remarks>
public sealed class FrameProtocol : IAsyncDisposable
{
    /// <summary>Maximum payload bytes accepted by <see cref="ReadFrameAsync"/> (DoS guard).</summary>
    /// <remarks>
    /// 16 MiB is comfortably above the largest Phase 4 video frame budget (~1 MiB)
    /// and well below LOH allocation limits. Any frame larger than this is treated
    /// as a malformed peer or framing desync.
    /// </remarks>
    public const int MaxFramePayloadBytes = 16 * 1024 * 1024;

    private readonly IDuplexStream _stream;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private bool _disposed;

    /// <summary>Wraps an open duplex stream with the crew-comms framing protocol.</summary>
    public FrameProtocol(IDuplexStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    /// <summary>
    /// Reads a single frame from the peer. Returns the frame type byte and
    /// the MessagePack-encoded payload bytes (zero-copy slice into a caller-
    /// owned buffer is NOT promised — callers should treat the returned array
    /// as owned).
    /// </summary>
    /// <exception cref="EndOfStreamException">Peer half-closed mid-frame.</exception>
    /// <exception cref="InvalidDataException">Payload exceeds <see cref="MaxFramePayloadBytes"/>.</exception>
    public async Task<(byte type, byte[] payload)> ReadFrameAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var header = new byte[5];
        await ReadExactlyAsync(header, ct).ConfigureAwait(false);

        var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
        if (length > MaxFramePayloadBytes)
            throw new InvalidDataException(
                $"Frame payload {length} exceeds maximum {MaxFramePayloadBytes} bytes.");

        var type = header[4];
        var payload = new byte[length];
        if (length > 0)
            await ReadExactlyAsync(payload, ct).ConfigureAwait(false);
        return (type, payload);
    }

    /// <summary>
    /// Writes a single frame to the peer. All concurrent calls are serialized
    /// behind the internal send gate.
    /// </summary>
    public async Task WriteFrameAsync(byte type, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (payload.Length > MaxFramePayloadBytes)
            throw new InvalidOperationException(
                $"Frame payload {payload.Length} exceeds maximum {MaxFramePayloadBytes} bytes.");

        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var header = new byte[5];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), (uint)payload.Length);
            header[4] = type;
            await _stream.WriteAsync(header, ct).ConfigureAwait(false);
            if (payload.Length > 0)
                await _stream.WriteAsync(payload, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>
    /// MessagePack-encodes <paramref name="payload"/> with the crew-comms
    /// resolver (RFC 4122 big-endian Guids) and writes one frame.
    /// </summary>
    public Task WriteAsync<T>(byte type, T payload, CancellationToken ct)
    {
        var bytes = MessagePackSerializer.Serialize(payload, CrewCommsResolver.Options);
        return WriteFrameAsync(type, bytes, ct);
    }

    /// <summary>
    /// Reads one frame and decodes its payload using the crew-comms MessagePack
    /// resolver. Returns the type byte alongside the decoded payload.
    /// </summary>
    public async Task<(byte type, T payload)> ReadAsync<T>(CancellationToken ct)
    {
        var (type, raw) = await ReadFrameAsync(ct).ConfigureAwait(false);
        var decoded = MessagePackSerializer.Deserialize<T>(raw, CrewCommsResolver.Options);
        return (type, decoded);
    }

    private async Task ReadExactlyAsync(Memory<byte> buffer, CancellationToken ct)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await _stream.ReadAsync(buffer[read..], ct).ConfigureAwait(false);
            if (n == 0)
                throw new EndOfStreamException("Peer closed the duplex stream mid-frame.");
            read += n;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _sendGate.Dispose();
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
