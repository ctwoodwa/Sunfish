using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// Host-supplied configuration for the AR cluster. Passed through
/// <see cref="DependencyInjection.FinancialArServiceCollectionExtensions.AddBlocksFinancialAr(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{BlocksFinancialArOptions}?)"/>
/// — installs configure their per-replica suffix here so customer-facing
/// invoice numbers like <c>INV-2026-05-17-CW-0001</c> render predictably.
/// </summary>
public sealed class BlocksFinancialArOptions
{
    /// <summary>
    /// Identifier for the local replica. Used as the per-replica suffix
    /// in invoice numbers; also serves as the arbiter on cross-replica
    /// number collisions. Defaults to the sentinel <c>"AA"</c>; hosts
    /// SHOULD override at install time so two devices on the same
    /// install can't mint the same number.
    /// </summary>
    public ReplicaId LocalReplicaId { get; init; } = new("AA");
}
