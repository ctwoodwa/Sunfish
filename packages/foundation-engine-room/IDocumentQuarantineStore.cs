using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Persistence seam for Engine Room quarantine operations per ADR 0079 §2.
/// Implementations are provided by the host (e.g., EF Core, InMemory for tests).
/// Registered via <see cref="EngineRoomServiceCollectionExtensions.AddEngineRoomQuarantineStore{TImpl}"/>.
/// </summary>
public interface IDocumentQuarantineStore
{
    /// <summary>Marks <paramref name="documentId"/> as quarantined for <paramref name="tenantId"/>.</summary>
    ValueTask<QuarantineResult> QuarantineAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        string reason,
        CancellationToken ct = default);

    /// <summary>Releases a quarantine record for <paramref name="documentId"/>.</summary>
    ValueTask<ReleaseResult> ReleaseAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        CancellationToken ct = default);

    /// <summary>Runs compaction on eligible documents for <paramref name="tenantId"/>.</summary>
    ValueTask<CompactionResult> CompactAsync(
        string documentId,
        TenantId tenantId,
        ActorId requestedBy,
        CancellationToken ct = default);
}
