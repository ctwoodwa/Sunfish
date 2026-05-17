using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Canonical party — a real human (<see cref="PartyKind.Person"/>) or an
/// organization (<see cref="PartyKind.Organization"/>) that other Sunfish
/// blocks attach roles to (customer, tenant, vendor, contractor, employee).
///
/// <para>
/// <b>Contact info is NOT inline.</b> Email, phone, and address each live in
/// their own append-only collection (<see cref="EmailAddress"/>,
/// <see cref="PhoneNumber"/>, <see cref="PartyAddress"/>) keyed by
/// <see cref="Id"/>. This shape lets a single party carry multiple emails /
/// phones / mailing addresses with stable history (per CRDT convention §4 —
/// supersedence via <c>ReplacedAt</c> rather than UPDATE).
/// </para>
///
/// <para>
/// <b>PII fields ship unencrypted in v1.</b> <see cref="TaxId"/> and
/// <see cref="DateOfBirth"/> sit on the public surface in plaintext; the
/// encryption-at-rest pass (ADR 0068 + W#37 OS-keychain landing) wraps them
/// at the persistence boundary without changing this API.
/// </para>
/// </summary>
public sealed record Party
{
    /// <summary>Stable identifier.</summary>
    public required PartyId Id { get; init; }

    /// <summary>Tenant scope — multi-tenant isolation handle.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Person or organization (immutable for the party's lifetime).</summary>
    public required PartyKind Kind { get; init; }

    /// <summary>How this party is displayed in lists, headers, conversation. Required.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Legal-name-on-document form (for organizations: as filed with the state). Optional for persons.</summary>
    public string? LegalName { get; init; }

    /// <summary>Preferred-name override for display (nicknames, chosen names). Persons only.</summary>
    public string? PreferredName { get; init; }

    // ── Person-shaped fields (all optional; organizations leave these null) ──
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? MiddleName { get; init; }
    public string? Suffix { get; init; }
    public string? Pronouns { get; init; }

    /// <summary>Date of birth (persons only). TODO: encrypt-at-rest per W#37/ADR-0068.</summary>
    public DateOnly? DateOfBirth { get; init; }

    // ── Organization-shaped fields (persons leave these null) ──
    public string? LegalEntityType { get; init; }

    /// <summary>Tax identifier — SSN, EIN, ITIN, foreign equivalent. TODO: encrypt-at-rest per W#37/ADR-0068.</summary>
    public string? TaxId { get; init; }

    /// <summary>Parent organization (allows org hierarchies; null for top-level orgs and all persons).</summary>
    public PartyId? ParentOrgId { get; init; }

    // ── Shared optional fields ──
    public string? WebSite { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? PreferredLanguage { get; init; }

    // ── Contact-preference flags (suppress communications without deleting contact rows) ──
    public required bool DoNotContact { get; init; }
    public required bool DoNotEmail { get; init; }
    public required bool DoNotCall { get; init; }
    public required bool DoNotSms { get; init; }

    // ── CRDT envelope (PR 3 wires IPartyWriteService to populate these) ──
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAt { get; init; }
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();

    /// <summary>
    /// Construct a freshly-minted party. The CRDT envelope (Version, RevisionVector,
    /// timestamps) is initialized to a clean baseline; downstream write services
    /// bump them on subsequent mutations.
    /// </summary>
    public static Party Create(
        TenantId tenantId,
        PartyKind kind,
        string displayName,
        PartyId createdBy,
        PartyId? id = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new Party
        {
            Id = id ?? PartyId.NewId(),
            TenantId = tenantId,
            Kind = kind,
            DisplayName = displayName,
            DoNotContact = false,
            DoNotEmail = false,
            DoNotCall = false,
            DoNotSms = false,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            Version = 1,
        };
    }
}
