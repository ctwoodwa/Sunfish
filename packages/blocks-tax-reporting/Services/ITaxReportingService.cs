using Sunfish.Blocks.TaxReporting.Models;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Core service contract for the tax-reporting domain.
/// </summary>
/// <remarks>
/// <para>
/// State-transition rules (enforced via <see cref="InvalidOperationException"/>):
/// <list type="bullet">
///   <item><see cref="FinalizeAsync"/> requires <see cref="TaxReportStatus.Draft"/>.</item>
///   <item><see cref="SignAsync"/> requires <see cref="TaxReportStatus.Finalized"/>.</item>
///   <item><see cref="AmendAsync"/> requires <see cref="TaxReportStatus.Signed"/> or <see cref="TaxReportStatus.Finalized"/>.</item>
/// </list>
/// </para>
/// <para>
/// G17 independence: this service accepts opaque input DTOs
/// (<see cref="ScheduleEGenerationRequest"/>, <see cref="Nec1099GenerationRequest"/>).
/// Consumers are responsible for translating G17 <c>JournalEntry</c> objects
/// into these DTOs in their application code. This keeps G17 and G18 independently mergeable.
/// </para>
/// </remarks>
public interface ITaxReportingService
{
    /// <summary>
    /// Generates a new Draft Schedule E report from the provided per-property rows.
    /// Aggregate totals (<see cref="ScheduleEBody.TotalRents"/>, etc.) are computed automatically.
    /// </summary>
    ValueTask<TaxReport> GenerateScheduleEAsync(
        ScheduleEGenerationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a new Draft Form 1099-NEC report.
    /// Recipients with <see cref="Nec1099Recipient.TotalPaid"/> below the $600 IRS threshold
    /// are filtered out automatically.
    /// </summary>
    ValueTask<TaxReport> Generate1099NecAsync(
        Nec1099GenerationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions a <see cref="TaxReportStatus.Draft"/> report to
    /// <see cref="TaxReportStatus.Finalized"/>, locking the content and
    /// computing the canonical-JSON SHA-256 hash into <see cref="TaxReport.SignatureValue"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">When <paramref name="id"/> is not found.</exception>
    /// <exception cref="InvalidOperationException">When the report is not in <see cref="TaxReportStatus.Draft"/>.</exception>
    ValueTask<TaxReport> FinalizeAsync(TaxReportId id, CancellationToken ct = default);

    /// <summary>
    /// Transitions a <see cref="TaxReportStatus.Finalized"/> report to
    /// <see cref="TaxReportStatus.Signed"/>, recording the consumer-provided signature value.
    /// </summary>
    /// <param name="id">The report to sign.</param>
    /// <param name="signatureValue">
    /// Consumer-provided signature string (e.g. an Ed25519 signature over the canonical-JSON hash,
    /// or a simple approval token). The service records this verbatim; it does not validate it.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">When <paramref name="id"/> is not found.</exception>
    /// <exception cref="InvalidOperationException">When the report is not in <see cref="TaxReportStatus.Finalized"/>.</exception>
    ValueTask<TaxReport> SignAsync(TaxReportId id, string signatureValue, CancellationToken ct = default);

    /// <summary>
    /// Initiates an amendment: transitions the existing report to
    /// <see cref="TaxReportStatus.Superseded"/> and returns a new
    /// <see cref="TaxReportStatus.Draft"/> that is a copy of the original body.
    /// </summary>
    /// <param name="id">The report to amend. Must be <see cref="TaxReportStatus.Signed"/> or <see cref="TaxReportStatus.Finalized"/>.</param>
    /// <param name="amendmentReason">Human-readable reason for the amendment (stored on the new Draft's body is not modified, but the reason is logged).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new Draft amendment report.</returns>
    /// <exception cref="KeyNotFoundException">When <paramref name="id"/> is not found.</exception>
    /// <exception cref="InvalidOperationException">When the report is not in a amendable status.</exception>
    ValueTask<TaxReport> AmendAsync(TaxReportId id, string amendmentReason, CancellationToken ct = default);

    /// <summary>Returns the report with the given <paramref name="id"/>, or <see langword="null"/>.</summary>
    ValueTask<TaxReport?> GetAsync(TaxReportId id, CancellationToken ct = default);

    /// <summary>Streams all reports matching the optional filter query.</summary>
    IAsyncEnumerable<TaxReport> ListAsync(
        ListTaxReportsQuery query,
        CancellationToken ct = default);
}
