using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Append-only row in a party's postal-address history. Wraps the
/// <see cref="Address"/> value object with party / tenant / label / validity
/// context and the standard CRDT envelope. Same supersedence semantics as
/// <see cref="EmailAddress"/> / <see cref="PhoneNumber"/>.
/// </summary>
public sealed record PartyAddress
{
    /// <summary>Stable identifier.</summary>
    public required PartyAddressId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The party this address belongs to.</summary>
    public required PartyId PartyId { get; init; }

    /// <summary>The postal address itself.</summary>
    public required Address Address { get; init; }

    /// <summary>Optional UI label: <c>"primary"</c>, <c>"mailing"</c>, <c>"billing"</c>, <c>"shipping"</c>, <c>"physical"</c>.</summary>
    public string? Label { get; init; }

    /// <summary>Marks the primary address — exactly one per party should be primary at any moment.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>When this address becomes valid (e.g., lease move-in). Optional — null = "valid since record creation".</summary>
    public Instant? ValidFrom { get; init; }

    /// <summary>When this address stops being valid (e.g., move-out). Optional — null = "still valid".</summary>
    public Instant? ValidTo { get; init; }

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

    /// <summary>Construct a fresh party-address row.</summary>
    public static PartyAddress Create(
        TenantId tenantId,
        PartyId partyId,
        Address address,
        bool isPrimary,
        PartyId createdBy,
        string? label = null,
        Instant? validFrom = null,
        Instant? validTo = null,
        PartyAddressId? id = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new PartyAddress
        {
            Id = id ?? PartyAddressId.NewId(),
            TenantId = tenantId,
            PartyId = partyId,
            Address = address,
            Label = label,
            IsPrimary = isPrimary,
            ValidFrom = validFrom,
            ValidTo = validTo,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            Version = 1,
        };
    }
}
