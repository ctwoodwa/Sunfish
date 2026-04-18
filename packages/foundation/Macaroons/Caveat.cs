namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// A first-party caveat — a textual predicate evaluated at verification time against a
/// <see cref="MacaroonContext"/>. Caveats can only attenuate (narrow) the bearer's authority;
/// they never widen it.
/// </summary>
/// <param name="Predicate">The caveat predicate text. See
/// <see cref="FirstPartyCaveatParser"/> for supported grammar.</param>
public readonly record struct Caveat(string Predicate)
{
    /// <summary>Returns the raw predicate text.</summary>
    public override string ToString() => Predicate;
}
