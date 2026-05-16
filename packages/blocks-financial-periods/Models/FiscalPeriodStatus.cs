namespace Sunfish.Blocks.FinancialPeriods.Models;

/// <summary>
/// Lifecycle state of a <see cref="FiscalPeriod"/> per Stage 02 §3.16.
/// </summary>
/// <remarks>
/// <para>
/// Canonical home for the period-status enum across the Sunfish
/// financial cluster. The <c>blocks-financial-ledger</c> package ships
/// a local placeholder of the same name (per the sibling ledger
/// hand-off's "Supporting stubs" section); that placeholder gets
/// deleted in PR 2 of this hand-off when the real
/// <c>SqlitePeriodResolver</c> + DI swap lands.
/// </para>
/// <para>
/// Status transitions follow Pattern A — Designated authority per
/// <c>_shared/engineering/crdt-friendly-schema-conventions.md</c> §7.
/// </para>
/// </remarks>
public enum FiscalPeriodStatus
{
    /// <summary>Postings allowed.</summary>
    Open,

    /// <summary>Postings blocked for regular users; admin (FinancialAdmin role) may bypass and may reopen.</summary>
    SoftClosed,

    /// <summary>Immutable; reopening requires explicit unlock-with-audit.</summary>
    Locked,
}
