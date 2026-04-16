// TODO(Task 7): Restore once Sunfish.Foundation.BusinessLogic.Enums is migrated.
// using Sunfish.Foundation.BusinessLogic.Enums;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models.ForwardRefs;

namespace Sunfish.Foundation.Models;

// ── Domain Models ──────────────────────────────────────────────────

/// <summary>
/// A single allocation record: one resource, one task, one time bucket.
/// The authoritative, persisted unit of data in the AllocationScheduler.
/// </summary>
public class AllocationRecord
{
    public Guid AllocationId { get; set; }
    public object ResourceId { get; set; } = default!;
    public object TaskId { get; set; } = default!;
    public string TaskName { get; set; } = string.Empty;
    public DateTime BucketStart { get; set; }
    public DateTime BucketEnd { get; set; }
    public decimal Value { get; set; }
    public AllocationUnit Unit { get; set; }
}

/// <summary>
/// A desired total for delta analysis. Stored separately from allocation records.
/// </summary>
public class AllocationTarget
{
    public Guid TargetId { get; set; }
    public object? ResourceId { get; set; }
    public object? TaskId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TargetValue { get; set; }
}

/// <summary>
/// A named collection of allocations -- either a locked Baseline or a divergent Scenario.
/// </summary>
public class AllocationSet
{
    public Guid SetId { get; set; }
    public string? DisplayLabel { get; set; }
    public string Name { get; set; } = string.Empty;
    public AllocationSetType Type { get; set; }
    public Guid? ParentBaselineId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public bool IsLocked { get; set; }
    public ScenarioStatus Status { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// A delta record for a scenario. Modifies, adds, or tombstones a baseline allocation.
/// </summary>
public class ScenarioOverride
{
    public Guid OverrideId { get; set; }
    public Guid SetId { get; set; }
    public Guid? OriginalAllocationId { get; set; }
    public AllocationRecord Override { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
}

/// <summary>Reference to a specific cell in the AllocationScheduler grid.</summary>
public class AllocationCellRef
{
    public object ResourceKey { get; set; } = default!;
    public object TaskId { get; set; } = default!;
    public DateTime BucketStart { get; set; }
    public DateTime BucketEnd { get; set; }
}

/// <summary>Context passed to CellTemplate RenderFragment.</summary>
public class AllocationCellContext
{
    public AllocationRecord? Record { get; set; }
    public object ResourceKey { get; set; } = default!;
    public DateTime BucketStart { get; set; }
    public DateTime BucketEnd { get; set; }
    public bool IsEditable { get; set; }
    public bool IsSelected { get; set; }
    public bool IsConflict { get; set; }
}

/// <summary>Descriptor for a custom context menu command.</summary>
public class AllocationMenuDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>A date range with start and end.</summary>
public class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

// ── EventArgs Classes ──────────────────────────────────────────────

/// <summary>Args for OnCellEdited -- single cell value committed.</summary>
public class CellEditedArgs
{
    public object ResourceKey { get; set; } = default!;
    public object TaskId { get; set; } = default!;
    public DateTime BucketStart { get; set; }
    public DateTime BucketEnd { get; set; }
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public AllocationRecord Record { get; set; } = default!;
}

/// <summary>Args for OnRangeEdited -- bulk range edit committed.</summary>
public class RangeEditedArgs
{
    public IReadOnlyList<AllocationRecord> AffectedRecords { get; set; } = Array.Empty<AllocationRecord>();
    public decimal Value { get; set; }
}

/// <summary>Args for OnContextMenuAction -- context menu command invoked.</summary>
public class ContextMenuActionArgs
{
    public string CommandName { get; set; } = string.Empty;
    public IReadOnlyList<AllocationCellRef> TargetCells { get; set; } = Array.Empty<AllocationCellRef>();
    public bool IsCancelled { get; set; }
}

/// <summary>Args for OnDistributeRequested -- distribution command initiated.</summary>
public class DistributeArgs
{
    public DateRange SourcePeriod { get; set; } = default!;
    public decimal TargetValue { get; set; }
    public TimeGranularity TargetGranularity { get; set; }
    public DistributionMode Mode { get; set; }
    public IReadOnlyList<AllocationRecord> ProposedDistribution { get; set; } = Array.Empty<AllocationRecord>();
    public bool IsCancelled { get; set; }
}

/// <summary>Args for OnShiftValues -- shift-forward or shift-backward.</summary>
public class ShiftValuesArgs
{
    public object ResourceKey { get; set; } = default!;
    public object TaskId { get; set; } = default!;
    public int Direction { get; set; }
    public int Periods { get; set; }
    public IReadOnlyList<AllocationRecord> AffectedRecords { get; set; } = Array.Empty<AllocationRecord>();
}

/// <summary>Args for OnMoveValues -- move-to-task or move-to-resource.</summary>
public class MoveValuesArgs
{
    public object SourceResourceKey { get; set; } = default!;
    public object TargetResourceKey { get; set; } = default!;
    public object? SourceTaskId { get; set; }
    public object? TargetTaskId { get; set; }
    public IReadOnlyList<AllocationRecord> AffectedRecords { get; set; } = Array.Empty<AllocationRecord>();
}

/// <summary>Args for OnTargetChanged -- desired total set or updated.</summary>
public class TargetChangedArgs
{
    public object ResourceKey { get; set; } = default!;
    public object? TaskId { get; set; }
    public DateRange Period { get; set; } = default!;
    public decimal TargetValue { get; set; }
}

/// <summary>Args for OnVisibleRangeChanged -- date range navigation.</summary>
public class VisibleRangeChangedArgs
{
    public DateTime NewStart { get; set; }
    public DateTime NewEnd { get; set; }
    public TimeGranularity ViewGrain { get; set; }
}

/// <summary>Args for OnSelectionChanged -- selected cell or range changes.</summary>
public class SelectionChangedArgs
{
    public IReadOnlyList<AllocationCellRef> SelectedCells { get; set; } = Array.Empty<AllocationCellRef>();
    public AllocationSelectionMode SelectionMode { get; set; }
}

/// <summary>Args for OnScenarioChanged -- active scenario switch.</summary>
public class ScenarioChangedArgs
{
    public Guid PreviousSetId { get; set; }
    public Guid NewSetId { get; set; }
}

/// <summary>Args for OnScenarioCreated -- new scenario created.</summary>
public class ScenarioCreatedArgs
{
    public AllocationSet NewSet { get; set; } = default!;
}

/// <summary>Args for OnAllocationOverridden -- edit produces an override.</summary>
public class AllocationOverriddenArgs
{
    public ScenarioOverride Override { get; set; } = default!;
    public Guid SetId { get; set; }
}

/// <summary>Args for OnScenarioStatusChanged -- scenario status transition.</summary>
public class ScenarioStatusChangedArgs
{
    public Guid SetId { get; set; }
    public ScenarioStatus OldStatus { get; set; }
    public ScenarioStatus NewStatus { get; set; }
}

/// <summary>Args for OnScenarioPromoted -- scenario promoted to baseline.</summary>
public class ScenarioPromotedArgs
{
    public Guid PromotedSetId { get; set; }
    public Guid NewBaselineSetId { get; set; }
}

/// <summary>Args for CanExecuteAction -- enable/disable context menu commands.</summary>
public class CanExecuteActionArgs
{
    public string CommandName { get; set; } = string.Empty;
    public IReadOnlyList<AllocationCellRef> TargetCells { get; set; } = Array.Empty<AllocationCellRef>();
    public bool IsEnabled { get; set; } = true;
}
