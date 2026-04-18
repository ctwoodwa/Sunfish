using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.Common;

/// <summary>
/// A federation peer identifier — the base64url-encoded form of the peer's Ed25519 public key
/// (a <see cref="PrincipalId"/>). Stable across peer restarts; identical peers have identical ids.
/// </summary>
/// <param name="Value">The base64url-encoded public-key string.</param>
public readonly record struct PeerId(string Value)
{
    /// <summary>Creates a <see cref="PeerId"/> from a Foundation <see cref="PrincipalId"/>.</summary>
    public static PeerId From(PrincipalId principal) => new(principal.ToBase64Url());

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Routing/connection descriptor for a remote peer — its identity, its current endpoint, and an
/// optional region hint for affinity-aware transports.
/// </summary>
/// <param name="Id">The stable peer identifier.</param>
/// <param name="Endpoint">The transport endpoint (e.g. HTTPS URL for the HTTP transport).</param>
/// <param name="Region">Optional region label for affinity-aware routing or diagnostics.</param>
public sealed record PeerDescriptor(PeerId Id, Uri Endpoint, string? Region = null);

/// <summary>
/// Unique identifier for a single sync message. Tests and traces use the "N" (32 hex char) form.
/// </summary>
/// <param name="Value">The underlying GUID.</param>
public readonly record struct SyncMessageId(Guid Value)
{
    /// <summary>Creates a fresh <see cref="SyncMessageId"/> backed by a new random GUID.</summary>
    public static SyncMessageId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Per-envelope nonce. Prevents replay by higher layers when envelope bodies are retained.
/// </summary>
/// <param name="Value">The underlying GUID.</param>
public readonly record struct Nonce(Guid Value)
{
    /// <summary>Creates a fresh <see cref="Nonce"/> backed by a new random GUID.</summary>
    public static Nonce NewNonce() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("N");
}
