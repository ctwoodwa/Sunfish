namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Cost-category classification for <see cref="ProjectBudgetLine"/>.</summary>
public enum BudgetCategory
{
    /// <summary>Labor hours.</summary>
    Labor,

    /// <summary>Materials / parts.</summary>
    Materials,

    /// <summary>Equipment rental.</summary>
    Equipment,

    /// <summary>Subcontracted scope.</summary>
    Subcontract,

    /// <summary>Permits / inspection fees.</summary>
    Permits,

    /// <summary>Contingency reserve (CapEx convention).</summary>
    Contingency,

    /// <summary>Other (catch-all).</summary>
    Other,
}
