namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Identifies a single version of an entity.
/// </summary>
/// <remarks>
/// <para>
/// Composed of <see cref="Entity"/>, an append-only <see cref="Sequence"/> (starting at 1),
/// and a <see cref="Hash"/> that chains to the previous version's hash. The hash is
/// deterministic over (parent-hash || canonical(body) || ISO-8601 valid-from), using
/// SHA-256; see spec §3.2 and plan D-CRDT-ROUTE.
/// </para>
/// <para>
/// <c>ToString()</c> renders a short form suitable for logs:
/// <c>"{entity}@{sequence}:{hash[..12]}"</c>.
/// </para>
/// </remarks>
public readonly record struct VersionId(EntityId Entity, int Sequence, string Hash)
{
    /// <inheritdoc />
    public override string ToString()
    {
        var shortHash = Hash.Length >= 12 ? Hash[..12] : Hash;
        return $"{Entity}@{Sequence}:{shortHash}";
    }
}
