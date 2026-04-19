using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Inspections.Models;
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
            Responses: []);

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
