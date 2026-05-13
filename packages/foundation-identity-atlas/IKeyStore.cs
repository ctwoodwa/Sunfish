using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Read-side access to actor identity profiles and key metadata per ADR 0066 §Phase 3.
/// Implementations read from the backing wallet/keystore without emitting audit events
/// (projection-only per ADR 0066 OQ-4). Real implementation ships in a future
/// wallet/keystore workstream; until then <see cref="NullKeyStore"/> is the default.
/// </summary>
public interface IKeyStore
{
    /// <summary>
    /// Returns the plain-text identity profile for <paramref name="actor"/> in
    /// <paramref name="tenant"/>. Returns null when no profile exists.
    /// MUST NOT call <c>IFieldDecryptor</c> (ADR 0066 OQ-4).
    /// </summary>
    ValueTask<IdentityProfile?> GetIdentityProfileAsync(
        TenantId tenant,
        ActorId actor,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current key metadata for <paramref name="actor"/> in
    /// <paramref name="tenant"/>. Returns null when no key has been registered.
    /// </summary>
    ValueTask<KeyInfo?> GetCurrentKeyInfoAsync(
        TenantId tenant,
        ActorId actor,
        CancellationToken ct = default);
}
