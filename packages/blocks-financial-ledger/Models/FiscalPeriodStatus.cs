namespace Sunfish.Blocks.FinancialLedger.Models;

/// <summary>
/// Status of a fiscal period per Stage 02 <c>blocks-financial-schema-design.md</c>
/// §6.1 period-gating. Local placeholder in PR 4 — relocate to
/// <c>blocks-financial-periods</c> when that cluster ships.
/// </summary>
public enum FiscalPeriodStatus
{
    /// <summary>Period accepts new entries from any user.</summary>
    Open,

    /// <summary>Period is closing — only users with the <c>FinancialAdmin</c> role can post.</summary>
    SoftClosed,

    /// <summary>Period is locked — no entries accepted from any user. Reversal must use a later open period.</summary>
    Locked,
}
