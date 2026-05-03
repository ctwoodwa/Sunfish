using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// The sequestration partition contract per ADR 0028-A5.4. Records (or
/// individual fields within records) registered here are tracked by
/// <see cref="IFormFactorMigrationService.ApplyMigrationAsync"/> for
/// sequester-on-surface-contraction + release-on-surface-expansion
/// transitions.
/// </summary>
public interface ISequestrationStore
{
    /// <summary>Registers a record so the migration service can manage its sequestration state.</summary>
    ValueTask RegisterAsync(SequesteredRecord record, CancellationToken ct = default);

    /// <summary>Marks a record as sequestered with the supplied flag.</summary>
    ValueTask SequesterAsync(string nodeId, string recordId, SequestrationFlagKind flag, CancellationToken ct = default);

    /// <summary>Marks a record as active (not sequestered). Used on derived-surface expansion per A5.4 rule 2.</summary>
    ValueTask ReleaseAsync(string nodeId, string recordId, CancellationToken ct = default);

    /// <summary>Returns every record tracked for <paramref name="nodeId"/>.</summary>
    ValueTask<IReadOnlyList<SequesteredRecord>> GetByNodeAsync(string nodeId, CancellationToken ct = default);

    /// <summary>Returns only the sequestered records for <paramref name="nodeId"/>.</summary>
    ValueTask<IReadOnlyList<SequesteredRecord>> GetSequesteredAsync(string nodeId, CancellationToken ct = default);
}
