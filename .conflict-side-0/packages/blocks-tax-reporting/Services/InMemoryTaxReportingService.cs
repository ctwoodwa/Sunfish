using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.TaxReporting.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Services;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="ITaxReportingService"/>.
/// Suitable for testing, prototyping, and kitchen-sink demos.
/// Not intended for production persistence — use a database-backed implementation for that.
/// </summary>
public sealed class InMemoryTaxReportingService : ITaxReportingService
{
    private readonly ConcurrentDictionary<TaxReportId, TaxReport> _reports = new();

    // Per-report locks for serializing concurrent state-mutating calls on the same report.
    private readonly ConcurrentDictionary<TaxReportId, SemaphoreSlim> _reportLocks = new();

    // ---------------------------------------------------------------------------
    // ITaxReportingService — generation
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public ValueTask<TaxReport> GenerateScheduleEAsync(
        ScheduleEGenerationRequest request,
        CancellationToken ct = default)
    {
        var totalRents    = request.Properties.Sum(p => p.RentsReceived);
        var totalExpenses = request.Properties.Sum(p => p.TotalExpenses);
        var netIncome     = totalRents - totalExpenses;

        var body = new ScheduleEBody(
            Properties: request.Properties,
            TotalRents: totalRents,
            TotalExpenses: totalExpenses,
            NetIncomeOrLoss: netIncome);

        var report = new TaxReport(
            Id: TaxReportId.NewId(),
            Year: request.Year,
            Kind: TaxReportKind.ScheduleE,
            PropertyId: request.PropertyId,
            Status: TaxReportStatus.Draft,
            GeneratedAtUtc: Instant.Now,
            SignatureValue: null,
            Body: body);

        _reports[report.Id] = report;
        return ValueTask.FromResult(report);
    }

    /// <inheritdoc />
    public ValueTask<TaxReport> Generate1099NecAsync(
        Nec1099GenerationRequest request,
        CancellationToken ct = default)
    {
        // Filter out recipients below the IRS $600 threshold.
        var qualifyingRecipients = request.Recipients
            .Where(r => r.MeetsThreshold)
            .ToList();

        var body = new Form1099NecBody(Recipients: qualifyingRecipients);

        var report = new TaxReport(
            Id: TaxReportId.NewId(),
            Year: request.Year,
            Kind: TaxReportKind.Form1099Nec,
            PropertyId: request.PropertyId,
            Status: TaxReportStatus.Draft,
            GeneratedAtUtc: Instant.Now,
            SignatureValue: null,
            Body: body);

        _reports[report.Id] = report;
        return ValueTask.FromResult(report);
    }

    // ---------------------------------------------------------------------------
    // ITaxReportingService — state transitions
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async ValueTask<TaxReport> FinalizeAsync(TaxReportId id, CancellationToken ct = default)
    {
        var reportLock = _reportLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await reportLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var report = RequireReport(id);
            RequireStatus(report, TaxReportStatus.Draft, nameof(FinalizeAsync));

            var signatureValue = TaxReportCanonicalJson.ComputeHash(report.Body);
            var finalized = report with
            {
                Status = TaxReportStatus.Finalized,
                SignatureValue = signatureValue,
            };
            _reports[id] = finalized;
            return finalized;
        }
        finally
        {
            reportLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<TaxReport> SignAsync(
        TaxReportId id,
        string signatureValue,
        CancellationToken ct = default)
    {
        var reportLock = _reportLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await reportLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var report = RequireReport(id);
            RequireStatus(report, TaxReportStatus.Finalized, nameof(SignAsync));

            var signed = report with
            {
                Status = TaxReportStatus.Signed,
                SignatureValue = signatureValue,
            };
            _reports[id] = signed;
            return signed;
        }
        finally
        {
            reportLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<TaxReport> AmendAsync(
        TaxReportId id,
        string amendmentReason,
        CancellationToken ct = default)
    {
        var reportLock = _reportLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await reportLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var report = RequireReport(id);

            if (report.Status is not (TaxReportStatus.Signed or TaxReportStatus.Finalized))
                throw new InvalidOperationException(
                    $"{nameof(AmendAsync)} requires status {TaxReportStatus.Signed} or " +
                    $"{TaxReportStatus.Finalized}, but report '{id}' is {report.Status}.");

            // Mark the original as Superseded.
            var superseded = report with { Status = TaxReportStatus.Superseded };
            _reports[id] = superseded;

            // Create a new Draft amendment — same body, fresh id.
            // The amendment reason is captured in the comment below; future passes may
            // surface it as a structured field (e.g. TaxReport.AmendmentReason).
            _ = amendmentReason; // consumed by caller intent; stored here as documentation

            var amendment = new TaxReport(
                Id: TaxReportId.NewId(),
                Year: report.Year,
                Kind: report.Kind,
                PropertyId: report.PropertyId,
                Status: TaxReportStatus.Draft,
                GeneratedAtUtc: Instant.Now,
                SignatureValue: null,
                Body: report.Body);

            _reports[amendment.Id] = amendment;
            return amendment;
        }
        finally
        {
            reportLock.Release();
        }
    }

    // ---------------------------------------------------------------------------
    // ITaxReportingService — queries
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public ValueTask<TaxReport?> GetAsync(TaxReportId id, CancellationToken ct = default)
    {
        _reports.TryGetValue(id, out var report);
        return ValueTask.FromResult(report);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TaxReport> ListAsync(
        ListTaxReportsQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var report in _reports.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.Year is not null && report.Year != query.Year)
                continue;
            if (query.Kind is not null && report.Kind != query.Kind)
                continue;
            if (query.Status is not null && report.Status != query.Status)
                continue;
            if (query.PropertyId is not null && report.PropertyId != query.PropertyId)
                continue;

            yield return report;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false); // satisfy async enumerator requirement
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private TaxReport RequireReport(TaxReportId id)
    {
        if (!_reports.TryGetValue(id, out var report))
            throw new KeyNotFoundException($"TaxReport '{id}' not found.");
        return report;
    }

    private static void RequireStatus(TaxReport report, TaxReportStatus expected, string operation)
    {
        if (report.Status != expected)
            throw new InvalidOperationException(
                $"{operation} requires status {expected}, but report '{report.Id}' is {report.Status}.");
    }
}
