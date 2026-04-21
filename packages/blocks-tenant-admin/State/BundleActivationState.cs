using Sunfish.Blocks.TenantAdmin.Models;

namespace Sunfish.Blocks.TenantAdmin.State;

/// <summary>
/// Blazor component state wrapper for the bundle-activation panel.
/// Holds the current list of active bundle activations for the visible tenant
/// plus the last error message surfaced from an activation attempt.
/// </summary>
/// <param name="ActiveActivations">The bundle activations currently active for the tenant.</param>
/// <param name="LastError">The most recent activation error to surface, or null for no error.</param>
public sealed record BundleActivationState(
    IReadOnlyList<BundleActivation> ActiveActivations,
    string? LastError)
{
    /// <summary>Creates an empty state with no active bundles and no error.</summary>
    public static BundleActivationState Empty() =>
        new(Array.Empty<BundleActivation>(), null);
}
