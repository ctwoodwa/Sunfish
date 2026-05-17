using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Capital-improvement specialization of <see cref="Project"/> per
/// Stage 02 §2.8. 1:1 with a <c>Project</c> whose <c>Kind ==
/// Remodel</c> — the parent constraint is enforced at the service
/// layer (PR 6). Captures permit + inspection metadata + the
/// capitalization workflow handed off to the financial cluster via
/// <c>Work.RemodelCapitalized</c>.
/// </summary>
/// <remarks>
/// Multi-phase WBS pattern informed by OpenProject (GPLv3) clean-room
/// study — no code copied; pattern only.
/// </remarks>
public sealed class RemodelProject
{
    public RemodelProjectId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ProjectId ProjectId { get; private set; }

    public string ScopeStatement { get; private set; } = string.Empty;
    public RemodelKind RemodelKind { get; private set; }

    public bool PermitRequired { get; private set; }
    public string? PermitNumber { get; private set; }
    public DateOnly? PermitIssuedAt { get; private set; }
    public IReadOnlyList<string> InspectionsRequired { get; private set; } = Array.Empty<string>();

    public Guid? CapitalizationAccountId { get; private set; }
    public DateOnly? PlacedInServiceAt { get; private set; }
    public Instant? CapitalizedAt { get; private set; }
    public decimal? CapitalizedAmount { get; private set; }
    public string? CapitalizedCurrency { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public Instant? DeletedAt { get; private set; }
    public long Version { get; private set; }

    /// <summary>Max length of <see cref="ScopeStatement"/> — DoS guard.</summary>
    public const int MaxScopeStatementLength = 4000;

    private RemodelProject() { }

    public static RemodelProject Create(
        TenantId tenantId,
        RemodelProjectId id,
        ProjectId projectId,
        string scopeStatement,
        RemodelKind remodelKind,
        bool permitRequired,
        IReadOnlyList<string>? inspectionsRequired,
        Guid createdBy,
        Instant createdAt)
    {
        if (string.IsNullOrWhiteSpace(scopeStatement))
            throw new ArgumentException("ScopeStatement is required.", nameof(scopeStatement));
        if (scopeStatement.Length > MaxScopeStatementLength)
            throw new ArgumentException(
                $"ScopeStatement exceeds MaxScopeStatementLength={MaxScopeStatementLength}.", nameof(scopeStatement));

        return new RemodelProject
        {
            Id                   = id,
            TenantId             = tenantId,
            ProjectId            = projectId,
            ScopeStatement       = scopeStatement,
            RemodelKind          = remodelKind,
            PermitRequired       = permitRequired,
            InspectionsRequired  = inspectionsRequired ?? Array.Empty<string>(),
            CreatedAt            = createdAt,
            UpdatedAt            = createdAt,
            CreatedBy            = createdBy,
            UpdatedBy            = createdBy,
        };
    }

    public void SetPermit(string permitNumber, DateOnly issuedAt, Guid updatedBy, Instant updatedAt)
    {
        if (!PermitRequired)
            throw new InvalidOperationException(
                "Cannot SetPermit when PermitRequired is false.");
        if (string.IsNullOrWhiteSpace(permitNumber))
            throw new ArgumentException("PermitNumber is required.", nameof(permitNumber));
        PermitNumber   = permitNumber;
        PermitIssuedAt = issuedAt;
        UpdatedBy      = updatedBy;
        UpdatedAt      = updatedAt;
        Version       += 1;
    }

    public void Capitalize(
        Guid capitalizationAccountId,
        DateOnly placedInServiceAt,
        decimal capitalizedAmount,
        string currency,
        Guid updatedBy,
        Instant capitalizedAt)
    {
        if (CapitalizedAt is not null)
            throw new InvalidOperationException("RemodelProject is already capitalized.");
        if (capitalizationAccountId == Guid.Empty)
            throw new ArgumentException("CapitalizationAccountId is required.", nameof(capitalizationAccountId));
        if (capitalizedAmount <= 0m)
            throw new ArgumentException("CapitalizedAmount must be > 0.", nameof(capitalizedAmount));
        var normalized = RemodelPhase.NormalizeCurrency(currency, nameof(currency));

        CapitalizationAccountId = capitalizationAccountId;
        PlacedInServiceAt       = placedInServiceAt;
        CapitalizedAt           = capitalizedAt;
        CapitalizedAmount       = capitalizedAmount;
        CapitalizedCurrency     = normalized;
        UpdatedBy               = updatedBy;
        UpdatedAt               = capitalizedAt;
        Version                += 1;
    }
}
