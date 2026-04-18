namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// Selects which version of an entity to read.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>Default (both null) → latest non-deleted version.</description></item>
///   <item><description><c>ExplicitSequence</c> → return exactly that version.</description></item>
///   <item><description><c>AsOf</c> → return the version whose validity range contains the given instant.</description></item>
/// </list>
/// If both are set, <c>ExplicitSequence</c> wins.
/// </remarks>
public readonly record struct VersionSelector(int? ExplicitSequence = null, DateTimeOffset? AsOf = null)
{
    /// <summary>Selector for the latest version (default).</summary>
    public static VersionSelector Latest => default;

    /// <summary>Selector for an explicit sequence number.</summary>
    public static VersionSelector AtSequence(int sequence) => new(ExplicitSequence: sequence);

    /// <summary>Selector for the version valid at a given instant.</summary>
    public static VersionSelector AtInstant(DateTimeOffset asOf) => new(AsOf: asOf);
}
