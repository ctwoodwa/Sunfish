using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>Options for a tenant data import.</summary>
public sealed record ImportOptions
{
    /// <summary>Target tenant; null means import into the system-default tenant.</summary>
    public TenantId? TargetTenantId { get; init; }

    /// <summary>Expected input format media type (defaults to JSON).</summary>
    public string Format { get; init; } = "application/json";

    /// <summary>When true, overwrite existing records with matching identities.</summary>
    public bool OverwriteExisting { get; init; }

    /// <summary>Module / scope keys to include. Empty means all present in the package.</summary>
    public IReadOnlyList<string> IncludeScopes { get; init; } = Array.Empty<string>();
}

/// <summary>Outcome of a tenant data import.</summary>
public sealed record ImportResult
{
    /// <summary>Number of records successfully imported.</summary>
    public int RecordsImported { get; init; }

    /// <summary>Number of records skipped (duplicates, scope filters, etc.).</summary>
    public int RecordsSkipped { get; init; }

    /// <summary>Number of records that failed to import.</summary>
    public int RecordsFailed { get; init; }

    /// <summary>Completion timestamp (UTC).</summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Non-fatal errors observed during import.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Tenant data import. Accepts an export-format package and merges it into
/// a target tenant. Module participation is a P2 follow-up contract.
/// </summary>
public interface IDataImportService
{
    /// <summary>Imports a package and returns a summary of the outcome.</summary>
    ValueTask<ImportResult> ImportAsync(
        Stream package,
        ImportOptions options,
        CancellationToken cancellationToken = default);
}
