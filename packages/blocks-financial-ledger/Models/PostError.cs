namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Structured failure modes for
/// <see cref="Services.IJournalPostingService.PostAsync"/> per Stage 02
/// <c>blocks-financial-schema-design.md</c> §6.1.
/// </summary>
public enum PostError
{
    /// <summary>Success — used as the sentinel "no error" value.</summary>
    None,

    /// <summary>Entry is not in <see cref="JournalEntryStatus.Draft"/> state.</summary>
    NotADraft,

    /// <summary>Entry has fewer than 2 lines (single-line journal posts are rejected).</summary>
    TooFewLines,

    /// <summary>Σ debits ≠ Σ credits (defense-in-depth — constructor also enforces).</summary>
    Imbalanced,

    /// <summary>A line references an account that <see cref="Services.IAccountResolver"/> cannot find.</summary>
    UnknownAccount,

    /// <summary>A line's account belongs to a different chart than the entry.</summary>
    WrongChart,

    /// <summary>A line posts to a header / summary account that has <see cref="GLAccount.IsPostable"/> = false.</summary>
    AccountNotPostable,

    /// <summary>A line currency disagrees with its account currency or the chart base currency.</summary>
    CurrencyMismatch,

    /// <summary>No fiscal period covers the entry date.</summary>
    NoPeriodForDate,

    /// <summary>The fiscal period for this date is <see cref="Services.IPeriodResolver.Status.Locked"/>.</summary>
    PeriodLocked,

    /// <summary>The fiscal period is <see cref="Services.IPeriodResolver.Status.SoftClosed"/> and the user is not a Financial Admin.</summary>
    PeriodSoftClosed,
}
