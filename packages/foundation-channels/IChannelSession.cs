using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Active crew-comms session between this host and a single remote peer.
/// Per ADR 0076.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle: <see cref="ChannelSessionState.Connecting"/> →
/// <see cref="ChannelSessionState.Active"/> →
/// <see cref="ChannelSessionState.Terminated"/>. Once terminated, the
/// session is single-use; <see cref="Completed"/> resolves with the
/// <see cref="ChannelTerminationReason"/>.
/// </para>
/// </remarks>
public interface IChannelSession : IAsyncDisposable
{
    /// <summary>The remote peer this session is bound to.</summary>
    PeerId Peer { get; }

    /// <summary>
    /// Negotiated capability — the highest common capability between the
    /// local <c>preferredCapabilities</c> and the remote peer's advertised
    /// capabilities at handshake time.
    /// </summary>
    ChannelCapability Capability { get; }

    /// <summary>Current lifecycle state.</summary>
    ChannelSessionState State { get; }

    /// <summary>
    /// Completes when the session reaches <see cref="ChannelSessionState.Terminated"/>.
    /// Await to observe the <see cref="ChannelTerminationReason"/> without
    /// a synchronous event handler.
    /// </summary>
    Task<ChannelTerminationReason> Completed { get; }

    /// <summary>
    /// Send a plain-text message. Returns when the frame is queued on the
    /// transport. Cancellation surfaces as
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task SendTextAsync(string message, CancellationToken ct);

    /// <summary>
    /// Stream incoming text frames. **Single-consumer only.** Enumerating
    /// from multiple consumers concurrently is undefined behavior;
    /// implementations MAY throw <see cref="InvalidOperationException"/>.
    /// </summary>
    IAsyncEnumerable<string> ReceiveTextAsync(CancellationToken ct);

    /// <summary>
    /// Send an Opus-encoded audio frame. Phase 3 of ADR 0076.
    /// **Implementations MUST throw <see cref="NotSupportedException"/>**
    /// when <see cref="Capability"/> does not include
    /// <see cref="ChannelCapability.Audio"/> — silent no-op is forbidden.
    /// Callers MUST check <see cref="Capability"/> before invoking.
    /// </summary>
    Task SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct);

    /// <summary>
    /// Stream incoming Opus audio frames. Phase 3 of ADR 0076.
    /// **Implementations MUST throw <see cref="NotSupportedException"/>**
    /// when <see cref="Capability"/> does not include
    /// <see cref="ChannelCapability.Audio"/>.
    /// </summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct);

    /// <summary>
    /// Send BYE, drain pending frames (up to 2s), then complete. If
    /// <see cref="IAsyncDisposable.DisposeAsync"/> is invoked without a
    /// prior <c>CloseAsync</c>, a best-effort BYE is sent fire-and-forget.
    /// </summary>
    Task CloseAsync(CancellationToken ct);
}
