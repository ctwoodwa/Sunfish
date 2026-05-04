using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Scope under which a <see cref="StandingOrder"/> applies. Per ADR 0065 §1.
/// </summary>
/// <remarks>
/// Scopes are not hierarchical at the type level; cross-scope effects (e.g., a
/// <see cref="Tenant"/>-scoped policy that affects <see cref="User"/>-scoped
/// settings) are encoded in validation pipeline rules, not in scope geometry.
/// JSON serialization uses the named string per ADR 0028 §A7.8 / cohort
/// precedent (W#34 / W#35 / W#36 / W#39 / W#40 / W#41).
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StandingOrderScope
{
    /// <summary>Per-user preference; affects the issuing user only.</summary>
    User,

    /// <summary>Per-tenant policy; affects all users of the tenant.</summary>
    Tenant,

    /// <summary>Platform-wide policy; spans tenants on the local node.</summary>
    Platform,

    /// <summary>Integration-config scope; targets a third-party connector or adapter.</summary>
    Integration,

    /// <summary>Security policy; locked-down scope requiring elevated authority.</summary>
    Security,
}
