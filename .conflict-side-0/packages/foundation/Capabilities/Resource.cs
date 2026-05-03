namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Opaque identifier for any addressable thing: an entity ID, a blob CID, a
/// namespace URI, or a composite URN. The capability graph treats resources
/// as black boxes keyed by string identity.
/// </summary>
public readonly record struct Resource(string Id)
{
    public override string ToString() => Id;
}
