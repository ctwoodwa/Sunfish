using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Models;

/// <summary>
/// Append-only row in a party's phone-number history. Stored in E.164 form
/// (<c>"+14155551234"</c>) so downstream SMS / calling integrations don't
/// each re-parse user-entered strings. Updates and deletes follow the same
/// CRDT-convention §4 supersedence pattern as <see cref="EmailAddress"/>.
/// </summary>
public sealed record PhoneNumber
{
    /// <summary>Stable identifier.</summary>
    public required PhoneNumberId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The party this number belongs to.</summary>
    public required PartyId PartyId { get; init; }

    /// <summary>The number in strict E.164: leading <c>+</c>, country code, subscriber number — no spaces, dashes, or parens.</summary>
    public required string E164 { get; init; }

    /// <summary>Optional extension (PBX, voicemail-skip). Kept separate from <see cref="E164"/> so dialers can build either form.</summary>
    public string? Extension { get; init; }

    /// <summary>Optional UI label: <c>"mobile"</c>, <c>"work"</c>, <c>"home"</c>, <c>"fax"</c>, <c>"other"</c>.</summary>
    public string? Label { get; init; }

    /// <summary>Marks the primary number — exactly one per party should be primary at any moment.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>True if SMS is supported on this line. Distinct from <see cref="Label"/> = "mobile" since some landlines support SMS and some mobile lines block it.</summary>
    public bool IsMobile { get; init; }

    /// <summary>When the recipient opted out of SMS specifically (STOP keyword, manual flag). Null while SMS-deliverable.</summary>
    public Instant? SmsOptedOutAt { get; init; }

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

    /// <summary>Construct a fresh phone-number row.</summary>
    public static PhoneNumber Create(
        TenantId tenantId,
        PartyId partyId,
        string e164,
        bool isPrimary,
        PartyId createdBy,
        string? label = null,
        string? extension = null,
        bool isMobile = false,
        PhoneNumberId? id = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new PhoneNumber
        {
            Id = id ?? PhoneNumberId.NewId(),
            TenantId = tenantId,
            PartyId = partyId,
            E164 = e164,
            Extension = extension,
            Label = label,
            IsPrimary = isPrimary,
            IsMobile = isMobile,
            CreatedAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now,
            Version = 1,
        };
    }
}
