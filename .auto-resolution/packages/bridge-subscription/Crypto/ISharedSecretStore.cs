using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Per-Anchor shared-secret store with rotation support per ADR
/// 0031-A1.12.1. 90-day default rotation cadence; 24-hour grace
/// window during which BOTH the previous secret AND the new secret
/// verify successfully. The Bridge side persists; the Anchor side
/// stores the secret per ADR 0046 substrate (encrypted at rest via
/// the foundation-recovery <c>IFieldEncryptor</c> when the host
/// wires it).
/// </summary>
public interface ISharedSecretStore
{
    /// <summary>Returns the active secret(s) for <paramref name="tenantId"/> — both the current AND a still-valid previous secret during the rotation grace window.</summary>
    ValueTask<SharedSecretLookup> ResolveAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Stages a rotation: replaces the current secret with
    /// <paramref name="newSecret"/>; the previous secret remains
    /// valid for the configured grace window
    /// (<see cref="InMemorySharedSecretStore.DefaultGraceWindow"/>
    /// = 24 hours per A1.12.1).
    /// </summary>
    ValueTask StageRotationAsync(string tenantId, string newSecret, CancellationToken ct = default);
}

/// <summary>Lookup result — the current secret + the previous secret if still in the grace window.</summary>
public sealed record SharedSecretLookup
{
    /// <summary>The current secret. May be null if the tenant has never registered.</summary>
    public string? Current { get; init; }

    /// <summary>The previous secret if still in the rotation grace window per A1.12.1; null otherwise.</summary>
    public string? PreviousInGrace { get; init; }

    /// <summary>Convenience: true iff <paramref name="candidate"/> matches either the current or the still-grace previous secret.</summary>
    public bool Matches(string? candidate) =>
        !string.IsNullOrEmpty(candidate) &&
        (string.Equals(candidate, Current, System.StringComparison.Ordinal) ||
         string.Equals(candidate, PreviousInGrace, System.StringComparison.Ordinal));
}
