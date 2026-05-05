using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Channels;

namespace Sunfish.Blocks.CrewComms.DependencyInjection;

/// <summary>
/// Builder surface returned by <see cref="ServiceCollectionExtensions.AddSunfishCrewComms"/>.
/// Provides the configurable knobs that don't fit cleanly in a single
/// <see cref="IServiceCollection"/> registration call. Per ADR 0076.
/// </summary>
public sealed class CrewCommsBuilder
{
    /// <summary>The underlying service collection — consumers MAY add their own registrations.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Creates a new builder bound to the supplied service collection.</summary>
    public CrewCommsBuilder(IServiceCollection services)
    {
        Services = services ?? throw new System.ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers an <see cref="InMemoryCrewRoster"/> seeded with the
    /// supplied crew members. Phase-1 single-tenant deployments use this;
    /// production deployments substitute a persistent roster.
    /// </summary>
    public CrewCommsBuilder AddInMemory(IEnumerable<CrewMember> seed)
    {
        Services.AddSingleton<ICrewRoster>(_ => new InMemoryCrewRoster(seed));
        return this;
    }
}
