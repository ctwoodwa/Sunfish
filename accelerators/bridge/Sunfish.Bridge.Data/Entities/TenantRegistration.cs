using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sunfish.Bridge.Data.Entities;

/// <summary>
/// Control-plane record for a commercial tenant of Bridge (ADR 0031, Wave 5.1).
/// Holds only operator-owned signup/billing/support metadata plus the team's
/// Ed25519 root public key. This entity is the authoritative source for
/// Bridge's shared control plane; it deliberately does <b>not</b> hold any
/// team data — team data lives in a per-tenant data plane (local-node-host,
/// Wave 5.2 pending).
/// </summary>
/// <remarks>
/// Per ADR 0031's "Control plane" subsection:
/// <list type="bullet">
///   <item>Records: <c>{tenant_id, plan, billing, support_contacts, team_public_key}</c>.</item>
///   <item>Never holds plaintext team data (paper §17.2 invariant).</item>
///   <item><see cref="ITenantContext"/> resolves tenants exclusively for control-plane concerns.</item>
/// </list>
/// </remarks>
public sealed class TenantRegistration
{
    /// <summary>Stable tenant identity. Matches the <see cref="ITenantContext.TenantId"/>
    /// string once the founder flow completes; before that, <see cref="Status"/> is
    /// <see cref="TenantStatus.Pending"/>.</summary>
    public Guid TenantId { get; set; } = Guid.NewGuid();

    /// <summary>URL-safe subdomain slug used for routing (<c>acme</c> →
    /// <c>acme.sunfish.example.com</c>). Lower-case, hyphenated, unique across
    /// all tenants. Wave 5.3 consumes this when building per-tenant browser
    /// shell subdomains.</summary>
    [Required]
    public string Slug { get; set; } = "";

    /// <summary>Human-facing display name (e.g. "Acme Corporation").</summary>
    [Required]
    public string DisplayName { get; set; } = "";

    /// <summary>Subscription tier. Stored as a string for billing-layer compatibility
    /// (see <c>blocks-subscriptions</c>); the in-code vocabulary is "Free", "Team",
    /// "Enterprise". Kept as <see cref="string"/> rather than enum so that marketing
    /// can rename tiers without a DB migration.</summary>
    [Required]
    public string Plan { get; set; } = "Free";

    /// <summary>Ed25519 root public key of the team's founder admin (32 bytes).
    /// <see langword="null"/> until the founder completes onboarding (browser-shell
    /// Wave 5.4). The operator never sees the matching private key — paper §17.2
    /// invariant.</summary>
    public byte[]? TeamPublicKey { get; set; }

    /// <summary>Trust level the tenant granted to the operator's hosted-node peer
    /// at signup. Default <see cref="TrustLevel.RelayOnly"/>: operator sees ciphertext
    /// only. See ADR 0031 "Data plane" subsection for the three options.</summary>
    public TrustLevel TrustLevel { get; set; } = TrustLevel.RelayOnly;

    /// <summary>Operator-owned signup/billing/support contacts for this tenant.
    /// Serialized as JSON in Postgres (jsonb) to avoid the overhead of a separate
    /// table for the common "a few email addresses" shape. Migrations can normalize
    /// this into a nav-property later if a richer support-ticket domain ships.</summary>
    public List<SupportContact> SupportContacts { get; set; } = [];

    /// <summary>Lifecycle status. <see cref="TenantStatus.Pending"/> until the founder
    /// flow persists <see cref="TeamPublicKey"/>; <see cref="TenantStatus.Active"/>
    /// after that. Billing transitions drive <see cref="TenantStatus.Suspended"/>
    /// (non-payment) and <see cref="TenantStatus.Cancelled"/> (tenant terminated).</summary>
    public TenantStatus Status { get; set; } = TenantStatus.Pending;

    /// <summary>Server-assigned UTC timestamp of the original <c>CreateAsync</c> call.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Updated by <c>TenantRegistry</c> on every mutation.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Operator-supplied free-text explanation of the most recent
    /// <see cref="TenantStatus.Suspended"/> transition (Wave 5.2.B). Cleared
    /// on <c>TenantRegistry.ResumeAsync</c>. <see langword="null"/> when the
    /// tenant is not (and has never been) suspended.</summary>
    /// <remarks>
    /// Capped at 500 characters — the field is a human-readable audit note
    /// (e.g. "billing non-payment Q2 2026", "operator hold during security
    /// review"), not a structured discriminator. Consumers wanting policy
    /// enforcement should layer their own vocabulary on top.
    /// </remarks>
    [MaxLength(500)]
    public string? SuspendedReason { get; set; }

    /// <summary>UTC timestamp of the most recent <see cref="TenantStatus.Cancelled"/>
    /// transition (Wave 5.2.B). <see langword="null"/> until
    /// <c>TenantRegistry.CancelAsync</c> has been called. Used by the Wave 5.2.C
    /// supervisor when naming the graveyard folder under
    /// <c>{TenantDataRoot}/graveyard/{TenantId:D}/{CancelledAt:yyyyMMdd-HHmmss}/</c>.</summary>
    public DateTime? CancelledAt { get; set; }
}

/// <summary>Operator-owned contact for billing or support. Not a team member —
/// team membership is a data-plane concern. See ADR 0031.</summary>
public sealed class SupportContact
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Billing"; // Billing | Technical | Admin
}

/// <summary>Operator-side lifecycle for a <see cref="TenantRegistration"/>.</summary>
public enum TenantStatus
{
    /// <summary>Signup flow started; founder has not yet supplied a team public key.</summary>
    Pending,

    /// <summary>Founder flow complete. Tenant is billable and reachable via its subdomain.</summary>
    Active,

    /// <summary>Billing failure or operator-initiated pause. Data-plane node is halted
    /// but data is retained pending <see cref="Cancelled"/> or reactivation.</summary>
    Suspended,

    /// <summary>Tenant has been terminated. Data-plane resources are released on a schedule
    /// owned by the operator (not defined by this entity).</summary>
    Cancelled,
}

/// <summary>Trust a tenant grants to the operator's hosted-node peer at signup
/// (ADR 0031 "Data plane" subsection). The paper §17.2 ciphertext-at-rest invariant
/// holds across all three levels; the levels differ only in whether the operator
/// can decrypt with tenant-issued role attestations.</summary>
public enum TrustLevel
{
    /// <summary>Default. Operator's relay stores ciphertext only; hosted-node peer
    /// is not issued a role attestation. Operator cannot decrypt team data.</summary>
    RelayOnly,

    /// <summary>Opt-in. Tenant admin issues a role attestation to the hosted-node
    /// peer, enabling backup verification and admin-assisted recovery. Operator
    /// can decrypt within the attested scope.</summary>
    AttestedHostedPeer,

    /// <summary>Self-hosted. Tenant runs their own local-node-host; Bridge provides
    /// only control-plane services (signup, billing, support). No hosted peer at all.</summary>
    NoHostedPeer,
}
