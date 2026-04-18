namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Canonical identifier for any entity in the Sunfish asset model.
/// </summary>
/// <remarks>
/// Wire form: <c>{Scheme}:{Authority}/{LocalPart}</c>. Example: <c>property:acme-rentals/42</c>.
/// Spec §3.1. <see cref="Parse"/> is the inverse of <see cref="ToString"/>.
/// </remarks>
public readonly record struct EntityId(string Scheme, string Authority, string LocalPart)
{
    /// <summary>Canonical string form: <c>{Scheme}:{Authority}/{LocalPart}</c>.</summary>
    public override string ToString() => $"{Scheme}:{Authority}/{LocalPart}";

    /// <summary>
    /// Parses the canonical string form back into an <see cref="EntityId"/>.
    /// </summary>
    /// <exception cref="FormatException">
    /// Thrown when the input does not match <c>{scheme}:{authority}/{localPart}</c> with
    /// all three segments non-empty.
    /// </exception>
    public static EntityId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var colon = value.IndexOf(':');
        if (colon <= 0 || colon == value.Length - 1)
            throw new FormatException($"EntityId '{value}' must be of form scheme:authority/localPart.");
        var scheme = value[..colon];
        var rest = value[(colon + 1)..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1)
            throw new FormatException($"EntityId '{value}' must be of form scheme:authority/localPart.");
        var authority = rest[..slash];
        var local = rest[(slash + 1)..];
        return new EntityId(scheme, authority, local);
    }

    /// <summary>Attempts to parse; returns false when the form is invalid.</summary>
    public static bool TryParse(string? value, out EntityId id)
    {
        try
        {
            if (value is null) { id = default; return false; }
            id = Parse(value);
            return true;
        }
        catch (FormatException)
        {
            id = default;
            return false;
        }
    }
}
