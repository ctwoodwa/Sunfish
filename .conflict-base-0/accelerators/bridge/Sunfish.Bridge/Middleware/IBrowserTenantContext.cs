using Sunfish.Bridge.Data.Entities;

namespace Sunfish.Bridge.Middleware;

/// <summary>
/// Browser-shell-specific tenant context, populated by
/// <see cref="TenantSubdomainResolutionMiddleware"/> from the request's
/// <c>Host</c> header (Wave 5.3.A — see
/// <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.A).
/// </summary>
/// <remarks>
/// <para>
/// This is distinct from the SaaS-posture <see cref="Sunfish.Foundation.Authorization.ITenantContext"/>
/// that backs the legacy PM-demo Razor pages and blocks-subscriptions/tenant-admin/
/// business-cases dependency injection. The browser shell — which speaks to the
/// hosted-node-per-tenant data plane — resolves its tenant from the request
/// subdomain, not from an OIDC claim, so the two contexts coexist in the
/// SaaS posture but MUST NOT be mixed in a single request pipeline.
/// </para>
/// <para>
/// Lifetime is scoped (matches the request). Callers must check
/// <see cref="IsResolved"/> before reading any other member; unresolved
/// reads throw <see cref="InvalidOperationException"/> so that pipeline
/// mis-ordering fails loudly rather than silently returning default values.
/// </para>
/// </remarks>
public interface IBrowserTenantContext
{
    /// <summary>True once the middleware has resolved the subdomain.</summary>
    bool IsResolved { get; }

    /// <summary>Stable tenant identity (matches
    /// <see cref="TenantRegistration.TenantId"/>).</summary>
    Guid TenantId { get; }

    /// <summary>URL-safe slug that identified the tenant in the Host header.</summary>
    string Slug { get; }

    /// <summary>Trust level the tenant granted the operator's hosted-node peer.
    /// Consumed by downstream Wave 5.3.C / 5.3.D sub-agents when deciding
    /// whether to open a proxied WebSocket or an ephemeral browser node.</summary>
    TrustLevel TrustLevel { get; }

    /// <summary>Ed25519 root public key of the team's founder admin, or
    /// <see langword="null"/> if the tenant has not yet completed the founder
    /// flow. Wave 5.3.B reads this when verifying passphrase-signed challenges.</summary>
    byte[]? TeamPublicKey { get; }

    /// <summary>Per-tenant Argon2id salt. 16 bytes once resolved. Served verbatim
    /// by the Wave 5.3.B <c>GET /auth/salt?slug=…</c> endpoint to the browser
    /// login page so its key-derivation is deterministic across attempts.</summary>
    byte[] AuthSalt { get; }

    /// <summary>
    /// Called by <see cref="TenantSubdomainResolutionMiddleware"/> after a
    /// successful tenant lookup. Not for general callers — other request-
    /// pipeline code reads the resolved values and must never overwrite them.
    /// Idempotent rebinding is not supported; a second call within the same
    /// request scope indicates a pipeline mis-configuration.
    /// </summary>
    void Bind(Guid tenantId, string slug, TrustLevel trustLevel, byte[]? teamPublicKey, byte[] authSalt);
}
