namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Outcome of an <see cref="ICapabilityGraph.MutateAsync"/> call. <see cref="Accepted"/>
/// means the op passed signature verification, authority validation, and was applied;
/// <see cref="Rejected"/> carries a human-readable <see cref="Reason"/>.
/// </summary>
/// <param name="Kind">Whether the mutation was accepted or rejected.</param>
/// <param name="Reason">When rejected, a short human-readable description.</param>
public sealed record MutationResult(MutationKind Kind, string? Reason = null)
{
    /// <summary>Singleton accepted result (no reason needed).</summary>
    public static MutationResult Accepted { get; } = new(MutationKind.Accepted);

    /// <summary>Builds a rejected result with the supplied reason.</summary>
    public static MutationResult Rejected(string reason) => new(MutationKind.Rejected, reason);
}

/// <summary>Discriminator for <see cref="MutationResult"/>.</summary>
public enum MutationKind
{
    /// <summary>The mutation was applied.</summary>
    Accepted,

    /// <summary>The mutation was rejected; see <see cref="MutationResult.Reason"/>.</summary>
    Rejected,
}
