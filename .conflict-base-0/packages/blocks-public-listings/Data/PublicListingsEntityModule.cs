using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.PublicListings.Data;

/// <summary>
/// <see cref="ISunfishEntityModule"/> contribution for the public-listings
/// block (per ADR 0015). Phase 1 ships no <c>IEntityTypeConfiguration</c>s
/// (in-memory only); the module is registered now so durable backends ship
/// by adding configs only.
/// </summary>
public sealed class PublicListingsEntityModule : ISunfishEntityModule
{
    /// <summary>The stable module key registered by this block.</summary>
    public const string Key = "sunfish.blocks.public-listings";

    /// <inheritdoc />
    public string ModuleKey => Key;

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublicListingsEntityModule).Assembly);
    }
}
