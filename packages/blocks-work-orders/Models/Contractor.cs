using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Vendor-side projection of a <c>blocks-people-*.Party</c> in the
/// contractor / vendor role per <c>blocks-work-schema-design.md</c>
/// §2.11. Contractor-specific fields (insurance, license, trades,
/// ratings) live here; the underlying party identity is referenced by
/// <see cref="PartyId"/>.
/// </summary>
// Contractor projection pattern inspired by Apache OFBiz Party/PartyRole (Apache 2.0) — clean-room expression.
public sealed class Contractor
{
    public ContractorId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid PartyId { get; private set; }
    public string DisplayName { get; private set; }
    public IReadOnlyList<TradeCategory> Trades { get; private set; }

    public string? LicenseNumber { get; private set; }
    public DateOnly? LicenseExpiresOn { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }
    public DateOnly? InsuranceExpiresOn { get; private set; }
    public decimal? BondedAmount { get; private set; }
    public string? BondedCurrency { get; private set; }
    public bool W9OnFile { get; private set; }
    public DateOnly? W9ReceivedOn { get; private set; }

    public bool PreferredFlag { get; private set; }
    public decimal? Rating { get; private set; }
    public int RatingCount { get; private set; }
    public decimal? HourlyRate { get; private set; }
    public string? HourlyRateCurrency { get; private set; }
    public bool EmergencyAvailable { get; private set; }

    public ContractorStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public long Version { get; private set; }

    private Contractor(
        ContractorId id,
        TenantId tenantId,
        Guid partyId,
        string displayName,
        IReadOnlyList<TradeCategory> trades,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        Id          = id;
        TenantId    = tenantId;
        PartyId     = partyId;
        DisplayName = displayName;
        Trades      = trades;
        Status      = ContractorStatus.Active;
        CreatedAt   = createdAt;
        UpdatedAt   = createdAt;
        CreatedBy   = createdBy;
        UpdatedBy   = createdBy;
        Version     = 0;
    }

    /// <summary>
    /// Build a new <see cref="Contractor"/> in
    /// <see cref="ContractorStatus.Active"/>.
    /// </summary>
    public static Contractor Create(
        TenantId tenantId,
        Guid partyId,
        string displayName,
        IReadOnlyList<TradeCategory> trades,
        Guid createdBy,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName must be non-empty.", nameof(displayName));
        ArgumentNullException.ThrowIfNull(trades);
        if (trades.Count == 0)
            throw new ArgumentException("At least one trade must be supplied.", nameof(trades));

        return new Contractor(
            id:          ContractorId.NewId(),
            tenantId:    tenantId,
            partyId:     partyId,
            displayName: displayName,
            trades:      trades.ToList(),
            createdAt:   createdAt ?? DateTimeOffset.UtcNow,
            createdBy:   createdBy);
    }

    /// <summary>Update license + insurance compliance fields together for atomicity.</summary>
    public void UpdateCompliance(
        string? licenseNumber, DateOnly? licenseExpiresOn,
        string? insurancePolicyNumber, DateOnly? insuranceExpiresOn,
        decimal? bondedAmount, string? bondedCurrency,
        bool w9OnFile, DateOnly? w9ReceivedOn,
        Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        LicenseNumber         = licenseNumber;
        LicenseExpiresOn      = licenseExpiresOn;
        InsurancePolicyNumber = insurancePolicyNumber;
        InsuranceExpiresOn    = insuranceExpiresOn;
        BondedAmount          = bondedAmount;
        BondedCurrency        = bondedCurrency;
        W9OnFile              = w9OnFile;
        W9ReceivedOn          = w9ReceivedOn;
        UpdatedBy             = updatedBy;
        UpdatedAt             = updatedAt ?? DateTimeOffset.UtcNow;
        Version              += 1;
    }

    /// <summary>Update operational fields (preferred flag, hourly rate, emergency availability).</summary>
    public void UpdateOperational(
        bool preferredFlag, decimal? hourlyRate, string? hourlyRateCurrency,
        bool emergencyAvailable, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        PreferredFlag        = preferredFlag;
        HourlyRate           = hourlyRate;
        HourlyRateCurrency   = hourlyRateCurrency;
        EmergencyAvailable   = emergencyAvailable;
        UpdatedBy            = updatedBy;
        UpdatedAt            = updatedAt ?? DateTimeOffset.UtcNow;
        Version             += 1;
    }

    /// <summary>Record a new rating; updates the running average + count.</summary>
    public void RecordRating(decimal score, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (score < 1m || score > 5m)
            throw new ArgumentOutOfRangeException(nameof(score), "Rating must be between 1 and 5 inclusive.");
        var prevTotal = (Rating ?? 0m) * RatingCount;
        RatingCount += 1;
        Rating       = (prevTotal + score) / RatingCount;
        UpdatedBy    = updatedBy;
        UpdatedAt    = updatedAt ?? DateTimeOffset.UtcNow;
        Version     += 1;
    }

    /// <summary>Transition to Paused.</summary>
    public void Pause(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        Status    = ContractorStatus.Paused;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Transition to Active from Paused or Blacklisted (re-instatement requires explicit unblacklist).</summary>
    public void Activate(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        Status    = ContractorStatus.Active;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Blacklist; not dispatched but record retained for vendor-history audit.</summary>
    public void Blacklist(string reason, Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason must be non-empty.", nameof(reason));
        Status    = ContractorStatus.Blacklisted;
        Notes     = $"{Notes}{(Notes is null ? "" : "\n")}Blacklisted: {reason}".Trim();
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>Archive — vendor closed; no longer trackable.</summary>
    public void Archive(Guid updatedBy, DateTimeOffset? updatedAt = null)
    {
        Status    = ContractorStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
        Version  += 1;
    }

    /// <summary>
    /// True when either <see cref="LicenseExpiresOn"/> or
    /// <see cref="InsuranceExpiresOn"/> is within
    /// <paramref name="warningDays"/> days of <paramref name="today"/>.
    /// Non-blocking — callers surface as a warning badge.
    /// </summary>
    public bool IsComplianceExpiringSoon(DateOnly today, int warningDays = 30)
    {
        var threshold = today.AddDays(warningDays);
        if (LicenseExpiresOn is { } lic && lic <= threshold) return true;
        if (InsuranceExpiresOn is { } ins && ins <= threshold) return true;
        return false;
    }
}
