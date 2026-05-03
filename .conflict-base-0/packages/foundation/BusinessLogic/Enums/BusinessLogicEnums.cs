namespace Sunfish.Foundation.BusinessLogic.Enums;

/// <summary>Controls whether a property can be read, written, or both.</summary>
public enum AccessMode
{
    /// <summary>No access — the property is hidden from the current principal.</summary>
    None = 0,

    /// <summary>The property can be read but not written.</summary>
    ReadOnly = 1,

    /// <summary>The property can be both read and written.</summary>
    ReadWrite = 2
}

/// <summary>The type of access being evaluated by an authorization rule.</summary>
public enum AuthorizationAction
{
    Read,
    Write
}

/// <summary>The outcome produced by a <see cref="Rules.IBusinessRule"/>.</summary>
public enum RuleOutcome
{
    /// <summary>The rule passed — no broken rule is emitted.</summary>
    Valid,

    /// <summary>The rule failed — a <see cref="Rules.BrokenRule"/> is emitted.</summary>
    Broken
}

/// <summary>
/// Lifecycle state of a scenario allocation set.
/// Used by the AllocationScheduler and any component that supports scenario planning.
/// </summary>
public enum ScenarioStatus
{
    /// <summary>Being edited; visible only to the creator.</summary>
    Draft,

    /// <summary>Open for team review.</summary>
    Shared,

    /// <summary>Frozen for stakeholder sign-off; no further edits.</summary>
    Approved,

    /// <summary>Merged into the baseline and archived.</summary>
    Promoted,

    /// <summary>Archived without promotion.</summary>
    Rejected
}

/// <summary>The category of an allocation set — baseline or divergent scenario.</summary>
public enum AllocationSetType
{
    /// <summary>The committed plan. Only one active baseline per project.</summary>
    Baseline,

    /// <summary>A divergent branch of allocations that overrides the baseline.</summary>
    Scenario
}
