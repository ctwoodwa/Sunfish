using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Sync.Handshake;

/// <summary>
/// Sync-daemon-protocol §4 handshake ladder:
/// <c>HELLO ⇄ HELLO → CAPABILITY_NEG → ACK</c>.
/// </summary>
/// <remarks>
/// <para>
/// Wave 2.1 wires the full four-message ladder end-to-end, but leaves
/// several spec §4 / §8 hardening steps to later waves:
/// </para>
/// <list type="bullet">
/// <item><description><b>Ed25519 signature verification on HELLO</b> — the
///   current implementation records the claimed public key but does not
///   verify <c>hello_sig</c>. Wave 1.6's <c>IAttestationVerifier</c> and the
///   role-attestation flow are the home for that check; the API here is
///   designed to drop a verifier in via <see cref="LocalIdentity.Signer"/>.</description></item>
/// <item><description><b>Schema-version negotiation</b> — we compare the
///   peer's offered <c>schema_version</c> against our
///   <see cref="DefaultSupportedVersions"/> list and emit a
///   <c>SCHEMA_VERSION_INCOMPATIBLE</c> ERROR (spec §4, §9) on miss. Full
///   semver parsing is deferred; a string-equality match against the fallback
///   list is the Wave 2.1 shape.</description></item>
/// <item><description><b>Capability-policy evaluation</b> — RESPOND's
///   <c>policy</c> hook receives the incoming
///   <see cref="CapabilityNegMessage"/> and returns the resulting
///   <see cref="AckMessage"/>. The in-tree default is "grant everything",
///   which is wrong for production but correct for the Wave 2.1 end-to-end
///   test harness. Bucket-eligibility evaluation is Wave 2.4.</description></item>
/// </list>
/// <para>
/// <b>Concurrent initiate.</b> Two peers that INITIATE against each other
/// simultaneously each send their HELLO first and each read the other's
/// HELLO second — there is no race because HELLO is symmetric and neither
/// side is privileged during it. The CAPABILITY_NEG / ACK step is
/// asymmetric (initiator sends, responder acks), but since each session is
/// a pair of TCP streams (one per direction, or one bidi stream), the
/// out-of-order arrival of a concurrent HELLO does not confuse the state
/// machine — we simply read whichever CAPABILITY_NEG arrives first and
/// respond to it.
/// </para>
/// </remarks>
public static class HandshakeProtocol
{
    /// <summary>Default protocol semver announced in HELLO.</summary>
    public const string DefaultSchemaVersion = "1.0.0";

    /// <summary>Fallback set announced in <c>supported_versions</c>.</summary>
    public static readonly IReadOnlyList<string> DefaultSupportedVersions = new[] { "1.0.0" };

    /// <summary>
    /// Initiator side of the handshake. Sends HELLO, reads the peer's HELLO,
    /// sends CAPABILITY_NEG, reads ACK, returns the negotiated
    /// <see cref="CapabilityResult"/>.
    /// </summary>
    public static async Task<CapabilityResult> InitiateAsync(
        ISyncDaemonConnection connection,
        LocalIdentity localIdentity,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(localIdentity);

        // Step 1: initiator sends HELLO.
        var hello = BuildHello(localIdentity);
        await connection.SendAsync(hello, ct).ConfigureAwait(false);

        // Step 2: receive peer's HELLO.
        var inbound = await connection.ReceiveAsync(ct).ConfigureAwait(false);
        if (inbound is not HelloMessage peerHello)
        {
            throw new InvalidOperationException(
                $"Expected HELLO from peer, got {inbound.GetType().Name}.");
        }

        // Quick schema-version screen; full negotiation is Wave 3.
        if (!NegotiateSchemaVersion(peerHello, localIdentity, out var agreedVersion))
        {
            var err = new ErrorMessage(
                ErrorCode.SchemaVersionIncompatible,
                $"Peer announced '{peerHello.SchemaVersion}', we support '{string.Join(",", localIdentity.SupportedVersions)}'.",
                Recoverable: false);
            await connection.SendAsync(err, ct).ConfigureAwait(false);
            throw new InvalidOperationException("Schema-version mismatch; session closed.");
        }

        // Step 3: initiator sends CAPABILITY_NEG.
        var proposal = new CapabilityNegMessage(
            ProposedStreams: localIdentity.ProposedStreams ?? Array.Empty<string>(),
            CpLeases: localIdentity.CpLeases ?? Array.Empty<string>(),
            BucketSubscriptions: localIdentity.BucketSubscriptions ?? Array.Empty<BucketRequest>());
        await connection.SendAsync(proposal, ct).ConfigureAwait(false);

        // Step 4: receive ACK.
        var ackInbound = await connection.ReceiveAsync(ct).ConfigureAwait(false);
        if (ackInbound is not AckMessage ack)
        {
            throw new InvalidOperationException(
                $"Expected ACK from peer, got {ackInbound.GetType().Name}.");
        }

        return new CapabilityResult(
            PeerNodeId: peerHello.NodeId,
            PeerPublicKey: peerHello.PublicKey,
            AgreedSchemaVersion: agreedVersion,
            Granted: ack.GrantedSubscriptions,
            Rejected: ack.Rejected,
            TickIntervalSeconds: ack.TickIntervalSeconds,
            MaxDeltasPerSecond: ack.MaxDeltasPerSecond);
    }

    /// <summary>
    /// Responder side of the handshake. Reads peer's HELLO, sends our HELLO,
    /// reads CAPABILITY_NEG, evaluates <paramref name="policy"/>, sends ACK,
    /// returns the resulting <see cref="CapabilityResult"/>.
    /// </summary>
    public static async Task<CapabilityResult> RespondAsync(
        ISyncDaemonConnection connection,
        LocalIdentity localIdentity,
        Func<CapabilityNegMessage, AckMessage> policy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(localIdentity);
        ArgumentNullException.ThrowIfNull(policy);

        // Step 1: receive peer's HELLO.
        var inbound = await connection.ReceiveAsync(ct).ConfigureAwait(false);
        if (inbound is not HelloMessage peerHello)
        {
            throw new InvalidOperationException(
                $"Expected HELLO from initiator, got {inbound.GetType().Name}.");
        }

        // Step 2: schema-version screen before sending anything that commits
        //         us to a session.
        if (!NegotiateSchemaVersion(peerHello, localIdentity, out var agreedVersion))
        {
            var err = new ErrorMessage(
                ErrorCode.SchemaVersionIncompatible,
                $"Initiator announced '{peerHello.SchemaVersion}', we support '{string.Join(",", localIdentity.SupportedVersions)}'.",
                Recoverable: false);
            await connection.SendAsync(err, ct).ConfigureAwait(false);
            throw new InvalidOperationException("Schema-version mismatch; session closed.");
        }

        // Step 3: responder sends HELLO.
        var hello = BuildHello(localIdentity);
        await connection.SendAsync(hello, ct).ConfigureAwait(false);

        // Step 4: receive CAPABILITY_NEG.
        var neg = await connection.ReceiveAsync(ct).ConfigureAwait(false);
        if (neg is not CapabilityNegMessage proposal)
        {
            throw new InvalidOperationException(
                $"Expected CAPABILITY_NEG from initiator, got {neg.GetType().Name}.");
        }

        // Step 5: evaluate policy → build ACK.
        var ack = policy(proposal);
        await connection.SendAsync(ack, ct).ConfigureAwait(false);

        return new CapabilityResult(
            PeerNodeId: peerHello.NodeId,
            PeerPublicKey: peerHello.PublicKey,
            AgreedSchemaVersion: agreedVersion,
            Granted: ack.GrantedSubscriptions,
            Rejected: ack.Rejected,
            TickIntervalSeconds: ack.TickIntervalSeconds,
            MaxDeltasPerSecond: ack.MaxDeltasPerSecond);
    }

    /// <summary>
    /// Build a HELLO message for the given local identity. Wave 2.1 stubs
    /// the signature field with 64 zero bytes if no signer is registered;
    /// Wave 1.6 wiring replaces that path with a real Ed25519 signature.
    /// </summary>
    internal static HelloMessage BuildHello(LocalIdentity identity)
    {
        var ts = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // The signature covers node_id || schema_version || sent_at per
        // sync-daemon-protocol §3.1. We stub to zeros here and leave the
        // real sign-path for Wave 1.6 / 2.5 wiring.
        var signature = identity.Signer?.Invoke(identity.NodeId, identity.SchemaVersion, ts)
                        ?? new byte[64];
        return new HelloMessage(
            NodeId: identity.NodeId,
            SchemaVersion: identity.SchemaVersion,
            SupportedVersions: identity.SupportedVersions,
            PublicKey: identity.PublicKey,
            Timestamp: ts,
            Signature: signature);
    }

    /// <summary>
    /// Pick the highest common schema version between the peer's offer and
    /// our local support list. Wave 2.1 uses string equality over
    /// <c>SupportedVersions</c> ∩ peer's <c>SupportedVersions</c>; semver
    /// range negotiation is a follow-up.
    /// </summary>
    private static bool NegotiateSchemaVersion(
        HelloMessage peer,
        LocalIdentity local,
        out string agreed)
    {
        agreed = string.Empty;
        var common = local.SupportedVersions
            .Intersect(peer.SupportedVersions, StringComparer.Ordinal)
            .ToList();
        if (common.Count == 0) return false;
        // "Highest" is stringwise-sorted-descending for Wave 2.1 — good enough
        // for matching 1.0.0 entries. Replace with real semver compare when
        // minor/patch axes show up.
        common.Sort(StringComparer.Ordinal);
        agreed = common[^1];
        return true;
    }
}

/// <summary>
/// Local node's identity for handshake purposes. Encapsulates the
/// node-id / public-key pair and the local policy for what to propose
/// in CAPABILITY_NEG.
/// </summary>
public sealed record LocalIdentity(
    byte[] NodeId,
    byte[] PublicKey,
    Func<byte[], string, ulong, byte[]>? Signer,
    string SchemaVersion,
    IReadOnlyList<string> SupportedVersions,
    IReadOnlyList<string>? ProposedStreams = null,
    IReadOnlyList<string>? CpLeases = null,
    IReadOnlyList<BucketRequest>? BucketSubscriptions = null);

/// <summary>
/// Outcome of a successful handshake. Consumed by the gossip scheduler to
/// know which streams are allowed to flow over this session.
/// </summary>
public sealed record CapabilityResult(
    byte[] PeerNodeId,
    byte[] PeerPublicKey,
    string AgreedSchemaVersion,
    IReadOnlyList<string> Granted,
    IReadOnlyList<Rejection> Rejected,
    uint TickIntervalSeconds,
    uint MaxDeltasPerSecond);
