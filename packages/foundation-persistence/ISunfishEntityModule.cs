using Microsoft.EntityFrameworkCore;

namespace Sunfish.Foundation.Persistence;

/// <summary>
/// Contract a <c>blocks-*</c> module implements to contribute its EF Core entity
/// configurations to the shared Bridge <c>DbContext</c>. See ADR 0015 for the
/// module-entity registration pattern this supports.
/// </summary>
/// <remarks>
/// Implementations typically delegate to <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>
/// against the block's own assembly so each entity's <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// class is discovered without per-entity boilerplate.
/// </remarks>
public interface ISunfishEntityModule
{
    /// <summary>
    /// Stable module key, reverse-DNS style (e.g. <c>sunfish.blocks.subscriptions</c>).
    /// Matches the module keys referenced by bundle manifests
    /// (<c>BusinessCaseBundleManifest.RequiredModules</c> / <c>OptionalModules</c>).
    /// </summary>
    string ModuleKey { get; }

    /// <summary>
    /// Applies the module's EF Core entity configurations to the shared
    /// <see cref="ModelBuilder"/>. Called once per <c>DbContext</c> model build,
    /// after the base <c>DbContext.OnModelCreating</c> has run.
    /// </summary>
    /// <param name="modelBuilder">The shared model builder.</param>
    void Configure(ModelBuilder modelBuilder);
}
