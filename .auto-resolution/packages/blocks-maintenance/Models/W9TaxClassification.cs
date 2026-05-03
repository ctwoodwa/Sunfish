namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// IRS W-9 federal-tax classification (W#18 Phase 4 / ADR 0058). Maps
/// to the W-9 form's "Federal tax classification" check-box section.
/// </summary>
public enum W9TaxClassification
{
    /// <summary>Sole proprietor / single-member LLC treated as disregarded entity.</summary>
    Individual,

    /// <summary>Limited-Liability Company (multi-member, default tax treatment).</summary>
    LLC,

    /// <summary>S-corporation election.</summary>
    SCorp,

    /// <summary>C-corporation election.</summary>
    CCorp,

    /// <summary>Partnership.</summary>
    Partnership,

    /// <summary>Trust or estate.</summary>
    Trust,

    /// <summary>Other (W-9 form free-text fallback).</summary>
    Other,
}
