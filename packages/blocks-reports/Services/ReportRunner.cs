using System;
using System.Collections.Generic;
using Sunfish.Blocks.Reports.Exceptions;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reports;

/// <summary>
/// Canonical <see cref="IReportRunner"/> implementation. Resolves
/// the cartridge from the registry, captures a snapshot marker,
/// invokes the cartridge inside a stopwatch, and extracts
/// provisionality from the result when it implements
/// <see cref="IReportProvisionalityCarrier"/>.
/// </summary>
public sealed class ReportRunner : IReportRunner
{
    private readonly ReportCartridgeRegistry _registry;
    private readonly ISnapshotMarkerSource _markers;
    private readonly TimeProvider _time;
    private readonly ReportRunnerOptions _options;

    /// <summary>Construct a runner bound to its collaborators.</summary>
    public ReportRunner(
        ReportCartridgeRegistry registry,
        ISnapshotMarkerSource markers,
        TimeProvider time,
        ReportRunnerOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);

        _registry = registry;
        _markers = markers;
        _time = time;
        _options = options;
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task<ReportRunResult<TResult>> RunAsync<TParams, TResult>(
        ReportKind kind,
        TParams parameters,
        TenantId tenantId,
        PrincipalId requestedBy,
        System.Threading.CancellationToken ct = default)
        where TParams : class
        where TResult : class
    {
        var cartridge = _registry.Resolve<TParams, TResult>(kind);

        var startedAt = _time.GetUtcNow();
        var marker = await _markers.CaptureAsync(tenantId, ct).ConfigureAwait(false);
        var context = new ReportExecutionContext(tenantId, marker, startedAt, requestedBy);

        TResult result;
        try
        {
            result = await cartridge.ExecuteAsync(context, parameters, ct).ConfigureAwait(false);
        }
        catch (ReportParameterValidationException)
        {
            // Pass through unwrapped — callers see the original parameter-error type
            // so they can render per-field validation messages.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Cooperative cancellation surfaces unwrapped per .NET conventions.
            throw;
        }
        catch (Exception ex)
        {
            throw new ReportCartridgeExecutionException(kind, ex);
        }
        var endedAt = _time.GetUtcNow();

        var (isProvisional, warnings) = ExtractProvisionality(result);
        if (warnings.Count > _options.MaxWarnings)
        {
            var truncated = new List<string>(_options.MaxWarnings);
            for (int i = 0; i < _options.MaxWarnings; i++) truncated.Add(warnings[i]);
            warnings = truncated;
        }

        return new ReportRunResult<TResult>(
            Kind: kind,
            Result: result,
            RunAtUtc: startedAt,
            SnapshotMarker: marker,
            RunDuration: endedAt - startedAt,
            IsProvisional: isProvisional,
            Warnings: warnings);
    }

    private static (bool IsProvisional, IReadOnlyList<string> Warnings) ExtractProvisionality<TResult>(TResult result)
    {
        if (result is IReportProvisionalityCarrier carrier)
            return (carrier.IsProvisional, carrier.Warnings);
        return (false, Array.Empty<string>());
    }
}
