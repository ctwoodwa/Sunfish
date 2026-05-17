namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Classification of a <see cref="Project"/> per Stage 02 §2.1.
/// </summary>
public enum ProjectKind
{
    /// <summary>Generic project (default).</summary>
    Generic,

    /// <summary>Unit / property remodel — typically capitalized; PR 5 adds the RemodelProject sidecar.</summary>
    Remodel,

    /// <summary>Capital expenditure tracking (CapEx).</summary>
    Capex,

    /// <summary>Unit-turnover project (between tenancies).</summary>
    Turnover,

    /// <summary>Capital improvement (broader CapEx category — landscaping, system upgrades).</summary>
    CapitalImprovement,
}
