using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MessagePack;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Transport;

namespace Sunfish.Blocks.CrewComms.Session;

/// <summary>
/// Native reference implementation of <see cref="IChannelSession"/>. Owns
/// the underlying <see cref="IDuplexStream"/> + <see cref="FrameProtocol"/>;
/// drives the SessionState machine; routes incoming frames to the appropriate
/// consumer; surfaces termination via <see cref="Completed"/>. Per ADR 0076.
/// </summary>
public sealed class NativeChannelSession : IChannelSession
{
    private static readonly TimeSpan KeepaliveSilenceThreshold = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CloseDrainBudget = TimeSpan.FromSeconds(2);

    private readonly FrameProtocol _frames;
    private readonly EncryptionHandshake _handshake;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _readerCts = new();
    private readonly TaskCompletionSource<ChannelTerminationReason> _completed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<string> _inboundText =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    // W#45 P4.5 — TYPING indicator stream. Bounded at 8 frames; drop
    // oldest so a typing storm doesn't grow unbounded if the UI is not
    // draining fast enough (pre-W#45-P4.5 substrate had no consumer at
    // all so silent-drop matches the prior surface).
    private readonly Channel<DateTimeOffset> _inboundTyping =
        Channel.CreateBounded<DateTimeOffset>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

    // DELIVERED receipts are unbounded — one per delivered TEXT, so
    // backpressure is naturally bounded by sent-message volume.
    private readonly Channel<Guid> _inboundDelivered =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly object _stateLock = new();

    private SessionState _state = SessionState.Idle;
    private long _lastSendTicks;
    private int _textReceiverCount;
    private Task? _readerTask;
    private bool _disposed;

    /// <inheritdoc />
    public PeerId Peer { get; }

    /// <inheritdoc />
    public ChannelCapability Capability { get; }

    /// <inheritdoc />
    public ChannelSessionState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state switch
                {
                    SessionState.Idle or SessionState.Inviting or SessionState.Confirming
                        => ChannelSessionState.Connecting,
                    SessionState.Active => ChannelSessionState.Active,
                    SessionState.Terminated => ChannelSessionState.Terminated,
                    _ => ChannelSessionState.Connecting,
                };
            }
        }
    }

    /// <inheritdoc />
    public Task<ChannelTerminationReason> Completed => _completed.Task;

    /// <summary>
    /// Creates a session bound to an already-handshook duplex stream. The
    /// caller (P4 SessionInitiator/Listener) performs HELLO/INVITE/ACCEPT/
    /// CONFIRM exchanges before constructing the session, then transfers
    /// ownership of the stream + handshake to this instance.
    /// </summary>
    public NativeChannelSession(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        PeerId peer,
        ChannelCapability negotiatedCapability,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(handshake);
        _frames = frames;
        _handshake = handshake;
        _time = time ?? TimeProvider.System;
        Peer = peer;
        Capability = negotiatedCapability;
        _lastSendTicks = _time.GetUtcNow().UtcTicks;
    }

    /// <summary>
    /// Transitions the session to <see cref="ChannelSessionState.Active"/>
    /// and starts the reader pump. Used by P4 signaling once CONFIRM is
    /// verified on both sides.
    /// </summary>
    public void Activate()
    {
        lock (_stateLock)
        {
            if (_state != SessionState.Idle && _state != SessionState.Confirming)
                throw new InvalidOperationException($"Cannot activate from state {_state}.");
            _state = SessionState.Active;
        }
        _readerTask = Task.Run(() => RunReaderAsync(_readerCts.Token));
    }

    /// <summary>Used by P4 signaling to advance the internal state machine.</summary>
    internal void TransitionTo(SessionState target)
    {
        lock (_stateLock)
        {
            _state = target;
        }
    }

    /// <inheritdoc />
    public async Task SendTextAsync(string message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureActive();
        var payload = MessagePackSerializer.Serialize(
            new TextPayload { MessageId = Guid.NewGuid(), Message = message },
            CrewCommsResolver.Options);
        await _frames.WriteFrameAsync(MessageType.Text, payload, ct).ConfigureAwait(false);
        Volatile.Write(ref _lastSendTicks, _time.GetUtcNow().UtcTicks);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ReceiveTextAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (Interlocked.Increment(ref _textReceiverCount) > 1)
        {
            Interlocked.Decrement(ref _textReceiverCount);
            throw new InvalidOperationException(
                "ReceiveTextAsync is single-consumer; multiple concurrent enumerations are forbidden.");
        }

        try
        {
            await foreach (var msg in _inboundText.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                yield return msg;
        }
        finally
        {
            Interlocked.Decrement(ref _textReceiverCount);
        }
    }

    /// <inheritdoc />
    public Task SendTypingAsync(CancellationToken ct)
    {
        EnsureActive();
        // Empty payload — TYPING frames carry no body per ADR 0076 §wire.
        return _frames.WriteFrameAsync(
            MessageType.Typing, ReadOnlyMemory<byte>.Empty, ct);
    }

    /// <inheritdoc />
    public Task SendDeliveredAsync(Guid messageId, CancellationToken ct)
    {
        EnsureActive();
        var bytes = new byte[16];
        RFC4122GuidFormatter.WriteBigEndian(bytes, messageId);
        return _frames.WriteFrameAsync(MessageType.Delivered, bytes, ct);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<DateTimeOffset> ReceiveTypingAsync(CancellationToken ct)
        => _inboundTyping.Reader.ReadAllAsync(ct);

    /// <inheritdoc />
    public IAsyncEnumerable<Guid> ReceiveDeliveredAsync(CancellationToken ct)
        => _inboundDelivered.Reader.ReadAllAsync(ct);

    /// <inheritdoc />
    public Task SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct)
        => throw new NotSupportedException(
            "Audio frames require ChannelCapability.Audio (Phase 3 of ADR 0076); not negotiated for this session.");

    /// <inheritdoc />
    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct)
        => throw new NotSupportedException(
            "Audio frames require ChannelCapability.Audio (Phase 3 of ADR 0076); not negotiated for this session.");

    /// <summary>
    /// Triggers an in-session keepalive HEARTBEAT if no frame has been sent
    /// for <see cref="KeepaliveSilenceThreshold"/>. Returns <c>true</c>
    /// when a keepalive was emitted. Called by <c>PresenceBus</c> once per
    /// tick.
    /// </summary>
    public async Task<bool> MaybeSendKeepaliveAsync(byte[] heartbeatPayload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(heartbeatPayload);
        if (State != ChannelSessionState.Active) return false;
        var now = _time.GetUtcNow().UtcTicks;
        var last = Volatile.Read(ref _lastSendTicks);
        if (TimeSpan.FromTicks(now - last) < KeepaliveSilenceThreshold) return false;

        await _frames.WriteFrameAsync(MessageType.Heartbeat, heartbeatPayload, ct).ConfigureAwait(false);
        Volatile.Write(ref _lastSendTicks, now);
        return true;
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken ct)
    {
        ChannelSessionState snapshot;
        lock (_stateLock)
        {
            snapshot = State;
            if (_state == SessionState.Terminated) return;
        }

        if (snapshot == ChannelSessionState.Active)
        {
            try
            {
                await _frames.WriteFrameAsync(MessageType.Bye, Array.Empty<byte>(), ct).ConfigureAwait(false);
            }
            catch
            {
                // BYE is best-effort; transport errors during close shouldn't mask the local-bye reason.
            }
            // 2-second drain budget — give the reader pump a chance to surface any final inbound frames.
            try
            {
                using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                drainCts.CancelAfter(CloseDrainBudget);
                await Task.WhenAny(_completed.Task, Task.Delay(CloseDrainBudget, drainCts.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* drain budget elapsed */ }
        }

        Terminate(ChannelTerminationReason.LocalBye);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (State != ChannelSessionState.Terminated)
        {
            // No prior CloseAsync — fire-and-forget BYE then immediately tear down.
            try
            {
                using var sendCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await _frames.WriteFrameAsync(MessageType.Bye, Array.Empty<byte>(), sendCts.Token).ConfigureAwait(false);
            }
            catch { /* best-effort fire-and-forget */ }
        }
        Terminate(ChannelTerminationReason.LocalBye);
        _readerCts.Cancel();
        try
        {
            if (_readerTask is not null)
                await _readerTask.ConfigureAwait(false);
        }
        catch { /* reader cancellation surfaces here */ }
        await _frames.DisposeAsync().ConfigureAwait(false);
        _readerCts.Dispose();
        _handshake.Dispose();
    }

    private async Task RunReaderAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (type, payload) = await _frames.ReadFrameAsync(ct).ConfigureAwait(false);
                switch (type)
                {
                    case MessageType.Text:
                        var text = MessagePackSerializer.Deserialize<TextPayload>(payload, CrewCommsResolver.Options);
                        await _inboundText.Writer.WriteAsync(text.Message, ct).ConfigureAwait(false);
                        break;
                    case MessageType.Bye:
                        Terminate(ChannelTerminationReason.RemoteBye);
                        return;
                    case MessageType.Heartbeat:
                    case MessageType.MuteState:
                        // Transient frames; consumed for liveness, not surfaced
                        // to ReceiveTextAsync / ReceiveTypingAsync /
                        // ReceiveDeliveredAsync.
                        break;
                    case MessageType.Typing:
                        // W#45 P4.5: surface as a wall-clock timestamp on the
                        // bounded TYPING stream. Empty-payload frame; no
                        // deserialization needed. TryWrite is intentional —
                        // BoundedChannelFullMode.DropOldest handles overflow.
                        _inboundTyping.Writer.TryWrite(_time.GetUtcNow());
                        break;
                    case MessageType.Delivered:
                        // W#45 P4.5: 16-byte RFC 4122 BE message-id. Defensive:
                        // malformed length silently dropped (the codec produces
                        // exactly 16 bytes; a foreign frame is the only path to
                        // here, and dropping is safer than throwing).
                        if (payload.Length == 16)
                        {
                            var guid = RFC4122GuidFormatter.ReadBigEndian(payload);
                            _inboundDelivered.Writer.TryWrite(guid);
                        }
                        break;
                    default:
                        // Unknown / out-of-band frames silently dropped — Phase 3+ may handle audio/video here.
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception)
        {
            Terminate(ChannelTerminationReason.TransportError);
            return;
        }
        finally
        {
            _inboundText.Writer.TryComplete();
            _inboundTyping.Writer.TryComplete();
            _inboundDelivered.Writer.TryComplete();
        }
    }

    private void EnsureActive()
    {
        var s = State;
        if (s != ChannelSessionState.Active)
            throw new InvalidOperationException($"Session is not Active (state = {s}).");
    }

    private void Terminate(ChannelTerminationReason reason)
    {
        lock (_stateLock)
        {
            if (_state == SessionState.Terminated) return;
            _state = SessionState.Terminated;
        }
        _inboundText.Writer.TryComplete();
        _completed.TrySetResult(reason);
    }
}
