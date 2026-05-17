using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.Models.Events;

// ── Cross-cluster event payload records per the event-bus catalog ───────
//
// Canonical event names (per cross-cluster-event-bus-design.md §2):
//   - Financial.TaxCodeAdded
//   - Financial.TaxCodeUpdated
//   - Financial.TaxRateAdded
//   - Financial.TaxRateExpired
//   - Reports.TaxFormLineMapEdited
//
// Wire shape: each payload is wrapped in a DomainEventEnvelope<T>
// (Services/DomainEventEnvelope.cs) before going through
// IDomainEventPublisher. See the envelope's idempotency-key
// convention for the per-event dedupe key shape.
//
// SchemaVersion convention: all records below are SchemaVersion=1.
// Bump when a field is added/removed/renamed.

/// <summary>
/// Payload for <c>Financial.TaxCodeAdded</c>. Emitted on first insert
/// of a <c>TaxCode</c> row (not on version-bump update — that's
/// <see cref="TaxCodeUpdated"/>).
/// </summary>
public sealed record TaxCodeAdded(
    TaxCodeId TaxCodeId,
    FL.ChartOfAccountsId ChartId,
    string Code,
    TaxKind Kind,
    TaxApplication Application);

/// <summary>
/// Payload for <c>Financial.TaxCodeUpdated</c>. Emitted on
/// <c>TaxCodeStore.UpsertAsync</c> when the row already existed —
/// i.e., on every <c>Version</c> bump.
/// </summary>
public sealed record TaxCodeUpdated(
    TaxCodeId TaxCodeId,
    FL.ChartOfAccountsId ChartId,
    int NewVersion);

/// <summary>
/// Payload for <c>Financial.TaxRateAdded</c>. Emitted on every
/// successful insert into <c>TaxRateLookup</c>, including the new
/// row that <c>SupersedeAsync</c> produces (which fires both a
/// <see cref="TaxRateExpired"/> for the old row and a
/// <see cref="TaxRateAdded"/> for the new — in that order).
/// </summary>
public sealed record TaxRateAdded(
    TaxRateId TaxRateId,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    decimal RatePercent,
    DateOnly EffectiveDate,
    FL.GLAccountId PayableAccountId);

/// <summary>
/// Payload for <c>Financial.TaxRateExpired</c>. Emitted when
/// <c>SupersedeAsync</c> closes out the prior open-ended rate with a
/// concrete <see cref="ExpiryDate"/>. Not emitted on standalone
/// upserts (since those don't close a prior rate).
/// </summary>
public sealed record TaxRateExpired(
    TaxRateId TaxRateId,
    TaxCodeId TaxCodeId,
    TaxJurisdictionId JurisdictionId,
    DateOnly ExpiryDate);

/// <summary>
/// Payload for <c>Reports.TaxFormLineMapEdited</c>. Emitted on
/// <c>TaxFormLineMapStore.UpsertAsync</c> when the row already
/// existed (the prior version was non-null) — i.e., on real edits,
/// not on first-insert / seed. The <c>Reports.*</c> namespace
/// prefix is correct per event-bus §3.5 (the mapping is
/// <i>consumed</i> by reports-tax even though its <i>storage</i> is
/// in this package — flagged in apps/docs).
/// </summary>
public sealed record TaxFormLineMapEdited(
    TaxFormLineMapId MapId,
    FL.ChartOfAccountsId ChartId,
    TaxFormKind FormKind,
    int TaxYear,
    string Line,
    IReadOnlyList<TaxAccountSelector> PriorSelectors,
    IReadOnlyList<TaxAccountSelector> NewSelectors,
    int NewVersion,
    string? EditedByPrincipalId);

/// <summary>
/// Canonical event-name constants — single source of truth for the
/// strings the envelope's <c>EventType</c> field carries. Consumers
/// can route off these without depending on payload types.
/// </summary>
public static class FinancialTaxEventNames
{
    public const string TaxCodeAdded         = "Financial.TaxCodeAdded";
    public const string TaxCodeUpdated       = "Financial.TaxCodeUpdated";
    public const string TaxRateAdded         = "Financial.TaxRateAdded";
    public const string TaxRateExpired       = "Financial.TaxRateExpired";
    public const string TaxFormLineMapEdited = "Reports.TaxFormLineMapEdited";
}
