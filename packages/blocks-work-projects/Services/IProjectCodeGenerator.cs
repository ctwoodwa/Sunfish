using System.Collections.Concurrent;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Mints stable, human-readable <c>Project.Code</c> values per
/// <c>crdt-friendly-schema-conventions.md</c> §1.8 (monotonic-per-
/// replica). Format: <c>PRJ-{yyyy}-{replicaSuffix}{seq:0000}</c>
/// (e.g., <c>PRJ-2026-L00001</c> for the first project minted on
/// the local replica in 2026).
/// </summary>
public interface IProjectCodeGenerator
{
    /// <summary>
    /// Mint the next project code for the supplied
    /// <paramref name="tenantId"/> + <paramref name="year"/>.
    /// </summary>
    Task<string> NextAsync(
        TenantId tenantId,
        int year,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory <see cref="IProjectCodeGenerator"/> for tests + the
/// kitchen-sink demo. Reads-and-increments a per-(tenant, year)
/// counter. Production replicas wire a foundation-localfirst-backed
/// implementation that persists the counter against the replica
/// record.
/// </summary>
public sealed class InMemoryProjectCodeGenerator : IProjectCodeGenerator
{
    /// <summary>
    /// Replica suffix per <c>crdt-friendly-schema-conventions.md</c>
    /// §1.8. Hard-coded "L0" (local-zero) until
    /// <c>foundation-localfirst</c> exposes an
    /// <c>IReplicaContext</c>; the real implementation reads the
    /// replica's 2-char ULID prefix.
    /// </summary>
    // TODO: wire IReplicaContext when foundation-localfirst exposes it (H5).
    private const string ReplicaSuffix = "L0";

    private readonly ConcurrentDictionary<(string TenantValue, int Year), int> _counters = new();

    /// <inheritdoc />
    public Task<string> NextAsync(TenantId tenantId, int year, CancellationToken cancellationToken = default)
    {
        if (year < 1970 || year > 9999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be 1970..9999.");

        var key = (tenantId.Value ?? string.Empty, year);
        var seq = _counters.AddOrUpdate(key, _ => 1, (_, prev) => prev + 1);
        var code = $"PRJ-{year:D4}-{ReplicaSuffix}{seq:D4}";
        return Task.FromResult(code);
    }
}
