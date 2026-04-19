using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>Lifecycle state of an export job.</summary>
public enum ExportState
{
    /// <summary>Export has been requested but not yet started.</summary>
    Pending = 0,

    /// <summary>Export is producing the output package.</summary>
    Running = 1,

    /// <summary>Export finished successfully; package available for download.</summary>
    Completed = 2,

    /// <summary>Export failed; see <see cref="ExportStatus.ErrorDetail"/>.</summary>
    Failed = 3,
}

/// <summary>Parameters for a tenant data export.</summary>
public sealed record ExportRequest
{
    /// <summary>Tenant whose data is being exported; null for system-scope exports.</summary>
    public TenantId? TenantId { get; init; }

    /// <summary>Requested output format media type (defaults to JSON).</summary>
    public string Format { get; init; } = "application/json";

    /// <summary>Module / scope keys to include. Empty means all.</summary>
    public IReadOnlyList<string> IncludeScopes { get; init; } = Array.Empty<string>();
}

/// <summary>Handle returned when an export is queued.</summary>
public sealed record ExportHandle
{
    /// <summary>Identifier for later status lookup and download.</summary>
    public required Guid ExportId { get; init; }

    /// <summary>Time the export was queued (UTC).</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Status of an export job.</summary>
public sealed record ExportStatus
{
    /// <summary>Export identifier.</summary>
    public required Guid ExportId { get; init; }

    /// <summary>Current state.</summary>
    public required ExportState State { get; init; }

    /// <summary>Progress percent in [0, 100].</summary>
    public double ProgressPercent { get; init; }

    /// <summary>Completion timestamp (UTC) when state is Completed or Failed.</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Error detail when state is Failed.</summary>
    public string? ErrorDetail { get; init; }
}

/// <summary>
/// Tenant data export. Asynchronous: the service queues a job, reports
/// progress, and streams the final package on demand. Module-level export
/// contributors plug in via a P2 follow-up contract.
/// </summary>
public interface IDataExportService
{
    /// <summary>Starts a tenant data export; returns a handle.</summary>
    ValueTask<ExportHandle> StartExportAsync(ExportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the current status of an export job.</summary>
    ValueTask<ExportStatus> GetStatusAsync(Guid exportId, CancellationToken cancellationToken = default);

    /// <summary>Opens a readable stream over the completed export package.</summary>
    ValueTask<Stream> OpenDownloadAsync(Guid exportId, CancellationToken cancellationToken = default);
}
