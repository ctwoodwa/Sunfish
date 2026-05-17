namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// Host-supplied configuration for the AP cluster. Passed through
/// <see cref="DependencyInjection.FinancialApServiceCollectionExtensions.AddBlocksFinancialAp(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{BlocksFinancialApOptions}?)"/>.
/// </summary>
public sealed class BlocksFinancialApOptions
{
    /// <summary>
    /// Optional dollar threshold at or above which a bill requires
    /// approval before payment can apply. <c>null</c> (default) means
    /// no approval gate — bills can transition Received → PartiallyPaid
    /// directly. When set, the payment service refuses to apply
    /// payments to <see cref="BillStatus.Received"/> bills with
    /// <c>Total &gt;= ApprovalThreshold</c> until they're explicitly
    /// transitioned to <see cref="BillStatus.Approved"/>.
    /// </summary>
    public decimal? ApprovalThreshold { get; init; }
}
