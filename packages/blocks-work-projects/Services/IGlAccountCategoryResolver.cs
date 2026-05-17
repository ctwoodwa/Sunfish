using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Optional resolver invoked by <c>JournalEntryPostedHandler</c>
/// to classify a GL account into a <see cref="BudgetCategory"/>. When
/// absent the handler falls back to <see cref="BudgetCategory.Other"/>.
/// Wire a concrete implementation from the financial cluster's
/// account-type taxonomy at the host's composition root.
/// </summary>
public interface IGlAccountCategoryResolver
{
    Task<BudgetCategory> ResolveAsync(
        TenantId tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}

internal sealed class FallbackGlAccountCategoryResolver : IGlAccountCategoryResolver
{
    public Task<BudgetCategory> ResolveAsync(TenantId tenantId, Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(BudgetCategory.Other);
}
