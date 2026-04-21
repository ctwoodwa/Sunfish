using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.TenantAdmin.Data;

/// <summary>
/// ADR 0015 entity module for <c>blocks-tenant-admin</c>. Contributes the block's
/// EF Core entity configurations (profiles, users, bundle activations) to the
/// shared Bridge <c>DbContext</c>.
/// </summary>
public sealed class TenantAdminEntityModule : ISunfishEntityModule
{
    /// <inheritdoc />
    public string ModuleKey => "sunfish.blocks.tenant-admin";

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantAdminEntityModule).Assembly);
    }
}
