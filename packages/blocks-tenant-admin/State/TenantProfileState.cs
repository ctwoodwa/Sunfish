using Sunfish.Blocks.TenantAdmin.Models;

namespace Sunfish.Blocks.TenantAdmin.State;

/// <summary>
/// Blazor component state wrapper for a single <see cref="TenantProfile"/>.
/// Holds the profile currently being viewed or edited, plus a last-loaded timestamp.
/// </summary>
/// <param name="Profile">The profile currently loaded into state, or null if not yet loaded.</param>
/// <param name="LoadedAtUtc">UTC timestamp of the most recent successful load, or null if never loaded.</param>
public sealed record TenantProfileState(TenantProfile? Profile, DateTime? LoadedAtUtc)
{
    /// <summary>Creates an empty state (no profile loaded).</summary>
    public static TenantProfileState Empty() => new(null, null);
}
