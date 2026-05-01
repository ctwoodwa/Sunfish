using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Signs + verifies <see cref="BridgeSubscriptionEvent"/>s per ADR
/// 0031-A1.2. The signature is computed over canonical-JSON bytes of
/// the event MINUS its <see cref="BridgeSubscriptionEvent.Signature"/>
/// field. Phase 1 ships HMAC-SHA256
/// (<see cref="HmacSha256EventSigner"/>); Ed25519 is reserved for
/// Phase 2+ migration per A1.12.2.
/// </summary>
public interface IEventSigner
{
    /// <summary>The algorithm this signer produces.</summary>
    SignatureAlgorithm Algorithm { get; }

    /// <summary>
    /// Returns the event with its <see cref="BridgeSubscriptionEvent.Signature"/>
    /// field populated (and <see cref="BridgeSubscriptionEvent.Algorithm"/>
    /// set to <see cref="Algorithm"/>). The returned event is canonical-JSON-
    /// stable and Anchor-verifiable.
    /// </summary>
    ValueTask<BridgeSubscriptionEvent> SignAsync(BridgeSubscriptionEvent unsigned, string sharedSecret, CancellationToken ct = default);

    /// <summary>
    /// Verifies the event's signature against <paramref name="sharedSecret"/>.
    /// Returns true iff the algorithm matches AND the recomputed
    /// signature byte-equals the supplied one. Constant-time comparison.
    /// </summary>
    ValueTask<bool> VerifyAsync(BridgeSubscriptionEvent signed, string sharedSecret, CancellationToken ct = default);
}
