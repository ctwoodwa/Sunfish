using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// Options controlling entity creation.
/// </summary>
/// <param name="Scheme">
/// Authority/scheme namespace for the generated <see cref="EntityId.Scheme"/> (e.g. <c>"entity"</c>,
/// <c>"property"</c>).
/// </param>
/// <param name="Authority">
/// Authority segment for the generated <see cref="EntityId.Authority"/> (tenant slug, org name).
/// </param>
/// <param name="Nonce">
/// Caller-supplied uniqueness token. Combined with <paramref name="Scheme"/>, <paramref name="Authority"/>
/// and the body to make <c>CreateAsync</c> idempotent.
/// </param>
/// <param name="Issuer">The creating actor (recorded in audit, folded into the ID derivation).</param>
/// <param name="Tenant">The tenant owning this entity.</param>
/// <param name="ValidFrom">
/// When the first version becomes valid. Defaults to <see cref="DateTimeOffset.UtcNow"/>.
/// </param>
/// <param name="ExplicitLocalPart">
/// If non-null, overrides the deterministic local-part derivation. Useful when an external
/// system already owns the identifier (e.g. migrating existing records).
/// </param>
public sealed record CreateOptions(
    string Scheme,
    string Authority,
    string Nonce,
    ActorId Issuer,
    TenantId Tenant,
    DateTimeOffset? ValidFrom = null,
    string? ExplicitLocalPart = null);
