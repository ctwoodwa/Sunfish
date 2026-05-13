using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Discriminated union expressing a multi-tenant query scope.
/// Per ADR 0084 §2.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Of(TenantId)"/> /
/// <see cref="Of(IEnumerable{TenantId})"/> factory overloads. Direct
/// case construction is allowed but prefer the factories for
/// forward-compat.
/// </para>
/// <para>
/// JSON serialization is NOT supported in Phase 1 —
/// <see cref="TenantSelection"/> is an application/query-layer type,
/// not a DTO. If a Bridge HTTP endpoint ever needs
/// <see cref="TenantSelection"/> as a query param, a custom
/// <c>JsonConverter</c> will be authored in a follow-up ADR
/// (deferred per ADR 0084 OQ-3).
/// </para>
/// </remarks>
public abstract record TenantSelection
{
    private TenantSelection() { }

    /// <summary>Exactly one tenant in scope.</summary>
    public sealed record ForSingle(TenantId TenantId) : TenantSelection;

    /// <summary>
    /// An explicit set of tenants in scope (≥1 member). Empty set
    /// throws at construction (both via the positional ctor and via
    /// the <see cref="IEnumerable{TenantId}"/> overload). Per ADR 0084
    /// §2 + W#1 WS-A security follow-up MF-5.
    /// </summary>
    public sealed record ForMultiple : TenantSelection
    {
        /// <summary>The set of tenants (≥1 member, by ctor invariant).</summary>
        public ImmutableArray<TenantId> TenantIds { get; }

        /// <summary>Construct from an immutable array; rejects empty input.</summary>
        public ForMultiple(ImmutableArray<TenantId> tenantIds)
        {
            if (tenantIds.IsDefaultOrEmpty)
            {
                throw new ArgumentException(
                    "ForMultiple requires at least one TenantId.", nameof(tenantIds));
            }
            TenantIds = tenantIds;
        }

        /// <summary>Construct from any enumerable; rejects empty input.</summary>
        public ForMultiple(IEnumerable<TenantId> tenantIds)
            : this(tenantIds.ToImmutableArray())
        {
        }

        /// <summary>Sequence-equal comparison so element order matters.</summary>
        public bool Equals(ForMultiple? other) =>
            other is not null && TenantIds.SequenceEqual(other.TenantIds);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var id in TenantIds) hash.Add(id);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// All tenants accessible to the requesting actor (platform-admin
    /// queries). Caller is responsible for verifying the actor holds
    /// the required capability before constructing this instance.
    /// System sentinels (TenantId values starting with <c>"__"</c>) are
    /// EXCLUDED from <see cref="Matches(TenantId)"/> — system records
    /// must never surface through tenant-listing queries even for
    /// platform admins. Per W#1 WS-A security follow-up MF-1.
    /// </summary>
    public sealed record AllAccessible : TenantSelection;

    /// <summary>
    /// Singleton convenience for <see cref="AllAccessible"/> — avoids
    /// per-call allocation in hot query paths. Per W#1 WS-A security
    /// follow-up MF-6 (ADR 0084 spec gap).
    /// </summary>
    public static readonly TenantSelection All = new AllAccessible();

    /// <summary>
    /// Factory: single tenant. Prefer over
    /// <c>new ForSingle(tenantId)</c> for forward-compat.
    /// </summary>
    public static TenantSelection Of(TenantId tenantId) => new ForSingle(tenantId);

    /// <summary>
    /// Factory: explicit set of tenants (≥1). Returns
    /// <see cref="ForSingle"/> when the input has exactly one element;
    /// <see cref="ForMultiple"/> otherwise. Throws on empty input.
    /// </summary>
    public static TenantSelection Of(IEnumerable<TenantId> tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        var arr = tenantIds.ToImmutableArray();
        if (arr.Length == 0)
        {
            throw new ArgumentException(
                "At least one TenantId required.", nameof(tenantIds));
        }
        return arr.Length == 1
            ? new ForSingle(arr[0])
            : new ForMultiple(arr);
    }

    /// <summary>
    /// Convenience varargs overload. Throws if zero arguments.
    /// </summary>
    public static TenantSelection Of(params TenantId[] tenantIds)
    {
        ArgumentNullException.ThrowIfNull(tenantIds);
        if (tenantIds.Length == 0)
        {
            throw new ArgumentException(
                "At least one TenantId required.", nameof(tenantIds));
        }
        return Of((IEnumerable<TenantId>)tenantIds);
    }

    /// <summary>
    /// Implicit cast from <see cref="TenantId"/> to
    /// <see cref="TenantSelection"/> (produces
    /// <see cref="ForSingle"/>). Lives on the target type to avoid a
    /// circular <c>foundation → foundation-multitenancy</c> package
    /// dependency.
    /// </summary>
    public static implicit operator TenantSelection(TenantId id) => new ForSingle(id);

    /// <summary>
    /// Returns true if this selection includes
    /// <paramref name="tenantId"/>. Used by in-memory stores and query
    /// engines; SQL implementations use the
    /// <see cref="ForSingle"/>/<see cref="ForMultiple"/> structural
    /// match + parameter array. Per ADR 0084 §2 (forward-compat for
    /// ADR 0085 WS-B).
    /// </summary>
    public bool Matches(TenantId tenantId) => this switch
    {
        ForSingle s => s.TenantId == tenantId,
        ForMultiple m => m.TenantIds.Contains(tenantId),
        AllAccessible => !tenantId.IsSystemSentinel,
        // The hierarchy is sealed (private base ctor + 3 nested-record
        // cases); the default arm is structurally unreachable. Throw
        // UnreachableException to document intent + match modern .NET
        // idiom (council M4).
        _ => throw new System.Diagnostics.UnreachableException(
            $"TenantSelection is sealed-hierarchy; unexpected case: {GetType().Name}"),
    };
}
