using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Crdt;
using Sunfish.Kernel.Sync.Application;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Phase 1 G2 — Anchor's bridge between the gossip daemon's
/// <see cref="IDeltaProducer"/> + <see cref="IDeltaSink"/> wire callbacks and
/// the active team's <see cref="ICrdtDocument"/>. Encodes outbound
/// CRDT-deltas the peer hasn't seen yet (Wave 2.5 send side) and applies
/// inbound deltas to the local document (Wave 2.5 receive side).
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 single-document convention: the daemon ships one logical document
/// per team (id <c>"default"</c>); the bridge holds a single
/// <see cref="ICrdtDocument"/> per Anchor process. Multi-document fan-out
/// (one document per business-MVP module — accounts, vendors, inventory,
/// projects) is a Phase 2 follow-up and is out of scope here.
/// </para>
/// <para>
/// The bridge implements both <see cref="IDeltaProducer"/> and
/// <see cref="IDeltaSink"/> with a single class because the active document
/// is the same for both directions. The DI registration in
/// <c>MauiProgram.cs</c> registers the singleton instance under both
/// interface keys so <c>AddSunfishKernelSync()</c> resolves the same
/// implementation for either role.
/// </para>
/// <para>
/// <b>Error handling:</b> per the <see cref="IDeltaSink"/> contract, malformed
/// deltas are recoverable — the implementation logs and returns rather than
/// throwing. Throwing here would abort the gossip round and trigger
/// dead-peer backoff for what may be a single bad frame.
/// </para>
/// </remarks>
public sealed class AnchorCrdtDeltaBridge : IDeltaProducer, IDeltaSink
{
    private readonly ICrdtDocument _document;
    private readonly ILogger<AnchorCrdtDeltaBridge> _logger;

    /// <summary>Construct the bridge over a single CRDT document.</summary>
    /// <param name="document">The active team's CRDT document. Phase 1 keeps
    /// a single document per Anchor process; Phase 2 will resolve per-team
    /// via <c>IActiveTeamAccessor</c>.</param>
    /// <param name="logger">Logger.</param>
    public AnchorCrdtDeltaBridge(ICrdtDocument document, ILogger<AnchorCrdtDeltaBridge> logger)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> EncodeOutboundDeltaAsync(
        string documentId,
        ReadOnlyMemory<byte> peerVectorClock,
        CancellationToken ct)
    {
        // Phase 1 single-document convention — ignore documentId mismatch
        // (the daemon always ships "default") and encode from our one doc.
        // Empty peerVectorClock encodes the full history; the gossip-level VC
        // carried in GOSSIP_PING isn't the CRDT-level VC, so richer per-peer
        // delta encoding is a follow-up wave.
        var bytes = _document.EncodeDelta(peerVectorClock);
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(bytes);
    }

    /// <inheritdoc />
    public ValueTask ApplyInboundDeltaAsync(
        string documentId,
        ulong opSequence,
        ReadOnlyMemory<byte> delta,
        CancellationToken ct)
    {
        try
        {
            _document.ApplyDelta(delta);
        }
        catch (Exception ex)
        {
            // IDeltaSink contract: malformed payloads are recoverable.
            _logger.LogWarning(ex,
                "Anchor CRDT bridge dropped inbound delta {DocumentId} op {OpSequence}: {Reason}",
                documentId, opSequence, ex.Message);
        }
        return ValueTask.CompletedTask;
    }
}
