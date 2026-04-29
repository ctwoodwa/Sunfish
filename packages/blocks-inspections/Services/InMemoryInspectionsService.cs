using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// In-memory implementation of <see cref="IInspectionsService"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> stores.
/// </summary>
/// <remarks>
/// Per-inspection state mutations (<see cref="StartAsync"/>, <see cref="RecordResponseAsync"/>,
/// <see cref="CompleteAsync"/>) are serialized via a per-inspection <see cref="SemaphoreSlim"/>
/// so concurrent calls on the same inspection cannot interleave.
/// <para>
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </para>
/// </remarks>
public sealed class InMemoryInspectionsService : IInspectionsService
{
    private readonly ConcurrentDictionary<InspectionTemplateId, InspectionTemplate> _templates = new();
    private readonly ConcurrentDictionary<InspectionId, Inspection> _inspections = new();
    private readonly ConcurrentDictionary<DeficiencyId, Deficiency> _deficiencies = new();
    private readonly ConcurrentDictionary<EquipmentConditionAssessmentId, EquipmentConditionAssessment> _conditionAssessments = new();

    // Per-inspection locks for state-mutating operations.
    private readonly ConcurrentDictionary<InspectionId, SemaphoreSlim> _inspectionLocks = new();

    // ── Templates ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<InspectionTemplate> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var template = new InspectionTemplate(
            Id: InspectionTemplateId.NewId(),
            Name: request.Name,
            Description: request.Description,
            Items: request.Items,
            CreatedAtUtc: Instant.Now);

        _templates[template.Id] = template;
        return ValueTask.FromResult(template);
    }

    /// <inheritdoc />
    public ValueTask<InspectionTemplate?> GetTemplateAsync(InspectionTemplateId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _templates.TryGetValue(id, out var template);
        return ValueTask.FromResult(template);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<InspectionTemplate> ListTemplatesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var template in _templates.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return template;
            await Task.Yield();
        }
    }

    // ── Inspections ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Inspection> ScheduleAsync(ScheduleInspectionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var inspection = new Inspection(
            Id: InspectionId.NewId(),
            TemplateId: request.TemplateId,
            UnitId: request.UnitId,
            InspectorName: request.InspectorName,
            ScheduledDate: request.ScheduledDate,
            Phase: InspectionPhase.Scheduled,
            StartedAtUtc: null,
            CompletedAtUtc: null,
            Responses: [],
            Trigger: request.Trigger);

        _inspections[inspection.Id] = inspection;
        return ValueTask.FromResult(inspection);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="InspectionPhase.Scheduled"/>.
    /// </exception>
    public async ValueTask<Inspection> StartAsync(InspectionId id, CancellationToken ct = default)
    {
        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Inspection '{id}' not found.");

            if (inspection.Phase != InspectionPhase.Scheduled)
                throw new InvalidOperationException(
                    $"Cannot start inspection '{id}': current phase is {inspection.Phase}, expected {InspectionPhase.Scheduled}.");

            var updated = inspection with
            {
                Phase = InspectionPhase.InProgress,
                StartedAtUtc = Instant.Now,
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="InspectionPhase.InProgress"/>.
    /// </exception>
    public async ValueTask<Inspection> RecordResponseAsync(
        InspectionId id,
        InspectionResponse response,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Inspection '{id}' not found.");

            if (inspection.Phase != InspectionPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot record a response for inspection '{id}': current phase is {inspection.Phase}, expected {InspectionPhase.InProgress}.");

            var updated = inspection with
            {
                Responses = [.. inspection.Responses, response],
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the inspection is not in <see cref="InspectionPhase.InProgress"/>.
    /// </exception>
    public async ValueTask<Inspection> CompleteAsync(InspectionId id, CancellationToken ct = default)
    {
        var sem = _inspectionLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(id, out var inspection))
                throw new InvalidOperationException($"Inspection '{id}' not found.");

            if (inspection.Phase != InspectionPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot complete inspection '{id}': current phase is {inspection.Phase}, expected {InspectionPhase.InProgress}.");

            var updated = inspection with
            {
                Phase = InspectionPhase.Completed,
                CompletedAtUtc = Instant.Now,
            };

            _inspections[id] = updated;
            return updated;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<Inspection?> GetInspectionAsync(InspectionId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _inspections.TryGetValue(id, out var inspection);
        return ValueTask.FromResult(inspection);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Inspection> ListInspectionsAsync(
        ListInspectionsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var inspection in _inspections.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.UnitId.HasValue && inspection.UnitId != query.UnitId.Value)
                continue;

            if (query.Phase.HasValue && inspection.Phase != query.Phase.Value)
                continue;

            yield return inspection;
            await Task.Yield();
        }
    }

    // ── Deficiencies ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public ValueTask<Deficiency> RecordDeficiencyAsync(RecordDeficiencyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var deficiency = new Deficiency(
            Id: DeficiencyId.NewId(),
            InspectionId: request.InspectionId,
            ItemId: request.ItemId,
            Severity: request.Severity,
            Description: request.Description,
            ObservedAtUtc: Instant.Now,
            Status: DeficiencyStatus.Open);

        _deficiencies[deficiency.Id] = deficiency;
        return ValueTask.FromResult(deficiency);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Deficiency> ListDeficienciesAsync(
        InspectionId inspectionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var deficiency in _deficiencies.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (deficiency.InspectionId != inspectionId)
                continue;

            yield return deficiency;
            await Task.Yield();
        }
    }

    // ── Equipment condition assessments (workstream #25 EXTEND) ─────────────

    /// <inheritdoc />
    public async ValueTask<EquipmentConditionAssessment> RecordEquipmentConditionAsync(
        RecordEquipmentConditionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sem = _inspectionLocks.GetOrAdd(request.InspectionId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_inspections.TryGetValue(request.InspectionId, out var inspection))
                throw new InvalidOperationException($"Inspection '{request.InspectionId}' not found.");

            if (inspection.Phase != InspectionPhase.InProgress)
                throw new InvalidOperationException(
                    $"Cannot record an equipment condition for inspection '{request.InspectionId}': current phase is {inspection.Phase}, expected {InspectionPhase.InProgress}.");

            var assessment = new EquipmentConditionAssessment
            {
                Id = EquipmentConditionAssessmentId.NewId(),
                InspectionId = request.InspectionId,
                EquipmentId = request.EquipmentId,
                Condition = request.Condition,
                ExpectedRemainingLifeYears = request.ExpectedRemainingLifeYears,
                Observations = request.Observations,
                Recommendations = request.Recommendations,
                ObservedAtUtc = Instant.Now,
            };

            _conditionAssessments[assessment.Id] = assessment;
            return assessment;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EquipmentConditionAssessment> ListEquipmentConditionsAsync(
        InspectionId inspectionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var assessment in _conditionAssessments.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (assessment.InspectionId != inspectionId)
                continue;
            yield return assessment;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EquipmentConditionAssessment> ListConditionHistoryForEquipmentAsync(
        EquipmentId equipmentId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Return chronological (oldest first) so consumers can walk the trend.
        var ordered = _conditionAssessments.Values
            .Where(a => a.EquipmentId.Equals(equipmentId))
            .OrderBy(a => a.ObservedAtUtc.Value)
            .ToList();

        foreach (var assessment in ordered)
        {
            ct.ThrowIfCancellationRequested();
            yield return assessment;
            await Task.Yield();
        }
    }

    // ── Move-in / move-out delta projection (workstream #25 EXTEND) ──────────

    /// <inheritdoc />
    public ValueTask<MoveInOutDelta?> GetMoveInOutDeltaAsync(EntityId unitId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Inspection? mostRecentMoveIn = null;
        Inspection? mostRecentMoveOut = null;

        foreach (var inspection in _inspections.Values)
        {
            if (inspection.UnitId != unitId)
                continue;

            if (inspection.Trigger == InspectionTrigger.MoveIn &&
                (mostRecentMoveIn is null || inspection.ScheduledDate > mostRecentMoveIn.ScheduledDate))
            {
                mostRecentMoveIn = inspection;
            }
            else if (inspection.Trigger == InspectionTrigger.MoveOut &&
                     (mostRecentMoveOut is null || inspection.ScheduledDate > mostRecentMoveOut.ScheduledDate))
            {
                mostRecentMoveOut = inspection;
            }
        }

        if (mostRecentMoveIn is null || mostRecentMoveOut is null)
            return ValueTask.FromResult<MoveInOutDelta?>(null);

        var responseDeltas = ComputeResponseDeltas(mostRecentMoveIn, mostRecentMoveOut);
        var conditionDeltas = ComputeConditionDeltas(mostRecentMoveIn.Id, mostRecentMoveOut.Id);

        return ValueTask.FromResult<MoveInOutDelta?>(new MoveInOutDelta(
            UnitId: unitId,
            MoveIn: mostRecentMoveIn,
            MoveOut: mostRecentMoveOut,
            ResponseDeltas: responseDeltas,
            EquipmentConditionDeltas: conditionDeltas));
    }

    private static IReadOnlyList<ResponseDelta> ComputeResponseDeltas(Inspection moveIn, Inspection moveOut)
    {
        var moveInByItem = moveIn.Responses
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Last().ResponseValue);
        var moveOutByItem = moveOut.Responses
            .GroupBy(r => r.ItemId)
            .ToDictionary(g => g.Key, g => g.Last().ResponseValue);

        var allItemIds = new HashSet<InspectionChecklistItemId>(moveInByItem.Keys);
        allItemIds.UnionWith(moveOutByItem.Keys);

        var deltas = new List<ResponseDelta>();
        foreach (var itemId in allItemIds)
        {
            var inValue = moveInByItem.TryGetValue(itemId, out var iv) ? iv : string.Empty;
            var outValue = moveOutByItem.TryGetValue(itemId, out var ov) ? ov : string.Empty;
            deltas.Add(new ResponseDelta(itemId, inValue, outValue, !string.Equals(inValue, outValue, StringComparison.Ordinal)));
        }
        return deltas;
    }

    private IReadOnlyList<EquipmentConditionDelta> ComputeConditionDeltas(InspectionId moveInId, InspectionId moveOutId)
    {
        var moveInByEquipment = _conditionAssessments.Values
            .Where(a => a.InspectionId == moveInId)
            .GroupBy(a => a.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ObservedAtUtc.Value).First().Condition);
        var moveOutByEquipment = _conditionAssessments.Values
            .Where(a => a.InspectionId == moveOutId)
            .GroupBy(a => a.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ObservedAtUtc.Value).First().Condition);

        // Only emit deltas for equipment present in BOTH inspections; partials aren't meaningful.
        var deltas = new List<EquipmentConditionDelta>();
        foreach (var (equipmentId, inCondition) in moveInByEquipment)
        {
            if (!moveOutByEquipment.TryGetValue(equipmentId, out var outCondition))
                continue;
            deltas.Add(new EquipmentConditionDelta(
                EquipmentId: equipmentId,
                MoveInCondition: inCondition,
                MoveOutCondition: outCondition,
                Degraded: outCondition > inCondition));  // enum order Good < Fair < Poor < Failed
        }
        return deltas;
    }

    // ── Reports ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown if the inspection does not exist.</exception>
    public async ValueTask<InspectionReport> GenerateReportAsync(InspectionId inspectionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_inspections.TryGetValue(inspectionId, out var inspection))
            throw new InvalidOperationException($"Inspection '{inspectionId}' not found.");

        // Count deficiencies linked to this inspection.
        var deficiencyCount = 0;
        await foreach (var _ in ListDeficienciesAsync(inspectionId, ct).ConfigureAwait(false))
            deficiencyCount++;

        // Resolve checklist items from the template to get TotalItems.
        var totalItems = 0;
        if (_templates.TryGetValue(inspection.TemplateId, out var template))
            totalItems = template.Items.Count;

        // Compute PassedItems: apply a per-kind pass heuristic.
        var passedItems = 0;
        if (template is not null)
        {
            var responsesByItemId = inspection.Responses
                .GroupBy(r => r.ItemId)
                .ToDictionary(g => g.Key, g => g.Last()); // last response wins if duplicated

            foreach (var item in template.Items)
            {
                if (!responsesByItemId.TryGetValue(item.Id, out var response))
                    continue;

                var passed = item.Kind switch
                {
                    InspectionItemKind.YesNo => string.Equals(response.ResponseValue, "yes", StringComparison.OrdinalIgnoreCase),
                    InspectionItemKind.PassFail => string.Equals(response.ResponseValue, "pass", StringComparison.OrdinalIgnoreCase),
                    InspectionItemKind.Rating1to5 => int.TryParse(response.ResponseValue, out var rating) && rating >= 3,
                    InspectionItemKind.FreeText => !string.IsNullOrWhiteSpace(response.ResponseValue),
                    InspectionItemKind.Photo => !string.IsNullOrWhiteSpace(response.ResponseValue),
                    _ => false,
                };

                if (passed)
                    passedItems++;
            }
        }

        var summary = $"Inspection {inspectionId} — {passedItems}/{totalItems} items passed, {deficiencyCount} deficiencies recorded.";

        var report = new InspectionReport(
            Id: InspectionReportId.NewId(),
            InspectionId: inspectionId,
            GeneratedAtUtc: Instant.Now,
            Summary: summary,
            TotalItems: totalItems,
            PassedItems: passedItems,
            DeficiencyCount: deficiencyCount);

        return report;
    }
}
