using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Append-only row in a party's email-address history. Updates are modeled
/// as "insert new row + mark old row <see cref="ReplacedAt"/>" rather than
/// in-place mutation (CRDT convention §4); deletes are tombstoned via
/// <see cref="DeletedAt"/> + <see cref="DeletedReason"/>. This preserves
/// the audit trail for "what was Jane's billing email on 2026-03-12?".
/// </summary>
public sealed record EmailAddress
{
    /// <summary>Stable identifier.</summary>
    public required EmailAddressId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The party this email belongs to.</summary>
    public required PartyId PartyId { get; init; }

    /// <summary>The address itself (RFC 5322).</summary>
    public required string Address { get; init; }

    /// <summary>Optional UI label: <c>"work"</c>, <c>"personal"</c>, <c>"billing"</c>, <c>"other"</c>.</summary>
    public string? Label { get; init; }

    /// <summary>Marks the primary address among multiple — exactly one per party should be primary at any moment.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>Whether the address has passed a confirmation (double-opt-in / click-tracking) flow.</summary>
    public bool IsValidated { get; init; }

    /// <summary>When the address was validated, if at all.</summary>
    public Instant? ValidatedAt { get; init; }

    /// <summary>When the recipient opted out (unsubscribe or bounce-suppression); null while deliverable.</summary>
    public Instant? OptedOutAt { get; init; }

    /// <summary>When this row was superseded by a newer one (CRDT append-only marker). Null while current.</summary>
    public Instant? ReplacedAt { get; init; }

    // ── CRDT envelope ──
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

    /// <summary>Construct a fresh email-address row.</summary>
    public static EmailAddress Create(
        TenantId tenantId,
        PartyId partyId,
        string address,
        bool isPrimary,
        PartyId createdBy,
        string? label = null,
        EmailAddressId? id = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new EmailAddress
        {
            Id = id ?? EmailAddressId.NewId(),
            TenantId = tenantId,
            PartyId = partyId,
            Address = address,
            Label = label,
            IsPrimary = isPrimary,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            Version = 1,
        };
    }
}
