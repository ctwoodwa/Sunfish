using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// A single crew-roster entry. Surfaced by <see cref="ICrewRoster.GetCrewAsync"/>
/// to drive presence resolution + invitation routing. Per ADR 0076.
/// </summary>
public sealed record CrewMember
{
    /// <summary>Federation peer identifier (base64url-encoded Ed25519 public key).</summary>
    public required PeerId Peer { get; init; }

    /// <summary>Human-readable display name shown in the operator's crew list.</summary>
    public required string DisplayName { get; init; }
}
