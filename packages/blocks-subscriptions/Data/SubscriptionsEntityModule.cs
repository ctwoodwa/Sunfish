using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.Subscriptions.Data;

/// <summary>
/// <see cref="ISunfishEntityModule"/> implementation that contributes the
/// subscription block's EF Core entity configurations to the shared Bridge
/// <c>DbContext</c>. See ADR 0015 for the module-entity registration pattern.
/// </summary>
public sealed class SubscriptionsEntityModule : ISunfishEntityModule
{
    /// <inheritdoc />
    public string ModuleKey => "sunfish.blocks.subscriptions";

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsEntityModule).Assembly);
    }
}
