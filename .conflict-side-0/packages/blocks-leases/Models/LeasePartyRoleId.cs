namespace Sunfish.Blocks.Leases.Models;

/// <summary>Stable identifier for a <see cref="LeasePartyRole"/> join entry.</summary>
/// <param name="Value">Underlying GUID.</param>
public readonly record struct LeasePartyRoleId(Guid Value)
{
    /// <summary>Mints a fresh GUID-backed id.</summary>
    public static LeasePartyRoleId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
