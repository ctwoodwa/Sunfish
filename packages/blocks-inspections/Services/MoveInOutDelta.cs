using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// Pair of move-in vs move-out inspections for a unit, with per-item response
/// deltas + per-equipment condition deltas. Returned by
/// <see cref="IInspectionsService.GetMoveInOutDeltaAsync"/>; consumed by
/// security-deposit reconciliation downstream.
/// </summary>
/// <param name="UnitId">The unit this delta is computed for.</param>
/// <param name="MoveIn">The most-recent <see cref="InspectionTrigger.MoveIn"/> inspection on the unit.</param>
/// <param name="MoveOut">The most-recent <see cref="InspectionTrigger.MoveOut"/> inspection on the unit.</param>
/// <param name="ResponseDeltas">Per-checklist-item deltas between the two inspections.</param>
/// <param name="EquipmentConditionDeltas">Per-equipment condition deltas between the two inspections.</param>
public sealed record MoveInOutDelta(
    EntityId UnitId,
    Inspection MoveIn,
    Inspection MoveOut,
    IReadOnlyList<ResponseDelta> ResponseDeltas,
    IReadOnlyList<EquipmentConditionDelta> EquipmentConditionDeltas);

/// <summary>
/// Delta between move-in and move-out responses for a single checklist item.
/// Items present in only one of the two inspections appear with the missing
/// side's value as <see cref="string.Empty"/>.
/// </summary>
/// <param name="ItemId">The checklist item this delta is for.</param>
/// <param name="MoveInValue">The response value at move-in (empty if not recorded).</param>
/// <param name="MoveOutValue">The response value at move-out (empty if not recorded).</param>
/// <param name="Changed"><see langword="true"/> when the two values differ (case-sensitive).</param>
public sealed record ResponseDelta(
    InspectionChecklistItemId ItemId,
    string MoveInValue,
    string MoveOutValue,
    bool Changed);

/// <summary>
/// Delta between move-in and move-out condition assessments for a single
/// equipment item.
/// </summary>
/// <param name="EquipmentId">The equipment this delta is for.</param>
/// <param name="MoveInCondition">Condition rating at move-in.</param>
/// <param name="MoveOutCondition">Condition rating at move-out.</param>
/// <param name="Degraded"><see langword="true"/> when the move-out condition is worse than the move-in condition (e.g. <see cref="ConditionRating.Good"/> → <see cref="ConditionRating.Fair"/>).</param>
public sealed record EquipmentConditionDelta(
    EquipmentId EquipmentId,
    ConditionRating MoveInCondition,
    ConditionRating MoveOutCondition,
    bool Degraded);
