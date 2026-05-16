using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// A tax code — the unit a journal entry, invoice line, or bill line
/// references when it carries tax — per Stage 02 §3.12.
///
/// <para>
/// The <c>Rates</c> sub-collection is intentionally <b>not</b> embedded
/// on the entity (per CRDT-conventions §4 "Append-only sub-collections
/// — never as an embedded array"). <see cref="TaxRateId"/>-keyed rows
/// land in PR 2 of the blocks-financial-tax-stage06-handoff as a
/// separate table linked by <c>TaxRate.TaxCodeId</c>; a derived
/// <c>Rates</c> accessor lives on <c>ITaxRateLookup</c>.
/// </para>
///
/// <para>
/// CRDT conventions applied:
/// </para>
/// <list type="bullet">
///   <item>§1 — string Guid id via <see cref="TaxCodeId"/>.</item>
///   <item>§2 — <see cref="DeletedAtUtc"/> tombstone.</item>
///   <item>§3 — <see cref="Version"/> bumped on each successful upsert.</item>
///   <item>§5 — <see cref="TaxKind"/> + <see cref="TaxApplication"/> stable string codes.</item>
/// </list>
/// </summary>
/// <param name="Id">Stable identity.</param>
/// <param name="ChartId">Owning chart of accounts (FK).</param>
/// <param name="Code">Human-stable label, e.g. <c>"US-VA-SALES"</c>, <c>"EU-DE-VAT19"</c>, <c>"EXEMPT"</c>.</param>
/// <param name="Name">Display label shown in the tax-code editor.</param>
/// <param name="Kind">Sales / VAT / GST / withholding / use / exempt / other.</param>
/// <param name="Application">OnSubtotal / Compound / Inclusive.</param>
/// <param name="IsActive">When false, the row is retained but new transactions can't reference it.</param>
/// <param name="Notes">Free-form operator notes.</param>
/// <param name="Version">CRDT §3 version vector. Starts at 1; bump on each upsert.</param>
/// <param name="CreatedAtUtc">When the row was first written.</param>
/// <param name="UpdatedAtUtc">When the row was last upserted.</param>
/// <param name="DeletedAtUtc">Tombstone — when set, the row is soft-deleted.</param>
public sealed record TaxCode(
    TaxCodeId Id,
    ChartOfAccountsId ChartId,
    string Code,
    string Name,
    TaxKind Kind,
    TaxApplication Application,
    bool IsActive = true,
    string? Notes = null,
    int Version = 1,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)
{
    /// <summary>
    /// Create a freshly-stamped <see cref="TaxCode"/> with a new id,
    /// version 1, and matching created/updated timestamps. Callers
    /// that already have a stable id (e.g. importing from ERPNext)
    /// should construct the record directly.
    /// </summary>
    public static TaxCode Create(
        ChartOfAccountsId chartId,
        string code,
        string name,
        TaxKind kind,
        TaxApplication application,
        string? notes = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxCode(
            Id: TaxCodeId.NewId(),
            ChartId: chartId,
            Code: code,
            Name: name,
            Kind: kind,
            Application: application,
            IsActive: true,
            Notes: notes,
            Version: 1,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
