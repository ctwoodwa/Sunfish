using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.BusinessCases.Data;

/// <summary>
/// <see cref="ISunfishEntityModule"/> contribution for the business-cases block.
/// Applies every <c>IEntityTypeConfiguration&lt;T&gt;</c> declared in this assembly
/// to the shared Bridge <c>DbContext</c> (see ADR 0015).
/// </summary>
public sealed class BusinessCasesEntityModule : ISunfishEntityModule
{
    /// <summary>The stable module key registered by this block.</summary>
    public const string Key = "sunfish.blocks.businesscases";

    /// <inheritdoc />
    public string ModuleKey => Key;

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BusinessCasesEntityModule).Assembly);
    }
}
