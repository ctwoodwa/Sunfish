using Sunfish.Blocks.Inspections.Models;

namespace Sunfish.Blocks.Inspections.Services;

/// <summary>
/// Contract for managing inspection templates, scheduled inspections, deficiency records,
/// and inspection reports.
/// </summary>
/// <remarks>
/// Deferred in this pass (G16 first pass):
/// <list type="bullet">
///   <item><description>Work-order rollup from deficiencies (blocks-maintenance, G16 second pass)</description></item>
///   <item><description>Offline mobile capture and photo/voice attachments</description></item>
///   <item><description>Event-bus integration and reactive triggers</description></item>
///   <item><description>BusinessRuleEngine hookup</description></item>
/// </list>
/// </remarks>
public interface IInspectionsService
{
    // ── Templates ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new inspection template from <paramref name="request"/> and returns the persisted record.
    /// </summary>
    /// <param name="request">Template creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="InspectionTemplate"/>.</returns>
    ValueTask<InspectionTemplate> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the template with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such template exists.
    /// </summary>
    /// <param name="id">The template identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<InspectionTemplate?> GetTemplateAsync(InspectionTemplateId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all known templates.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<InspectionTemplate> ListTemplatesAsync(CancellationToken ct = default);

    // ── Inspections ───────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a new inspection from <paramref name="request"/> and returns the created record.
    /// The new inspection is always in <see cref="InspectionPhase.Scheduled"/>.
    /// </summary>
    /// <param name="request">Inspection scheduling payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Inspection> ScheduleAsync(ScheduleInspectionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions the inspection from <see cref="InspectionPhase.Scheduled"/> to
    /// <see cref="InspectionPhase.InProgress"/> and records <c>StartedAtUtc</c>.
    /// </summary>
    /// <param name="id">The inspection to start.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="InspectionPhase.Scheduled"/>.</exception>
    ValueTask<Inspection> StartAsync(InspectionId id, CancellationToken ct = default);

    /// <summary>
    /// Appends <paramref name="response"/> to the inspection's response list.
    /// The inspection must be in <see cref="InspectionPhase.InProgress"/>.
    /// </summary>
    /// <param name="id">The inspection to record a response for.</param>
    /// <param name="response">The response to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="InspectionPhase.InProgress"/>.</exception>
    ValueTask<Inspection> RecordResponseAsync(InspectionId id, InspectionResponse response, CancellationToken ct = default);

    /// <summary>
    /// Transitions the inspection from <see cref="InspectionPhase.InProgress"/> to
    /// <see cref="InspectionPhase.Completed"/> and records <c>CompletedAtUtc</c>.
    /// </summary>
    /// <param name="id">The inspection to complete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection is not in <see cref="InspectionPhase.InProgress"/>.</exception>
    ValueTask<Inspection> CompleteAsync(InspectionId id, CancellationToken ct = default);

    /// <summary>
    /// Returns the inspection with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such inspection exists.
    /// </summary>
    /// <param name="id">The inspection identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Inspection?> GetInspectionAsync(InspectionId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all inspections matching <paramref name="query"/>.
    /// Pass <see cref="ListInspectionsQuery.Empty"/> to return all inspections.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Inspection> ListInspectionsAsync(ListInspectionsQuery query, CancellationToken ct = default);

    // ── Deficiencies ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records a new deficiency linked to an inspection and returns the created record.
    /// </summary>
    /// <param name="request">Deficiency creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Deficiency> RecordDeficiencyAsync(RecordDeficiencyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams all deficiencies associated with <paramref name="inspectionId"/>.
    /// </summary>
    /// <param name="inspectionId">The inspection whose deficiencies to stream.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Deficiency> ListDeficienciesAsync(InspectionId inspectionId, CancellationToken ct = default);

    // ── Reports ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a summary <see cref="InspectionReport"/> for the given inspection.
    /// Can be called at any point but is most meaningful after the inspection is completed.
    /// </summary>
    /// <param name="inspectionId">The inspection to summarise.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inspection does not exist.</exception>
    ValueTask<InspectionReport> GenerateReportAsync(InspectionId inspectionId, CancellationToken ct = default);
}
