using System;
using Sunfish.Federation.Common;

namespace Sunfish.Blocks.CrewComms.Signaling;

/// <summary>
/// Resolves the "glare" condition where two peers concurrently send each
/// other an INVITE. Per ADR 0076: deterministic ordering by ordinal
/// comparison of <see cref="PeerId.Value"/> — the lower-valued peer yields
/// (cancels its outbound INVITE) so the higher-valued peer becomes the
/// initiator. Both peers compute identical results without coordination.
/// </summary>
public static class GlareResolver
{
    /// <summary>
    /// Returns <c>true</c> when the local peer should yield its outbound
    /// INVITE in favor of the remote peer's. Both peers running this
    /// independently agree because the comparison is total and
    /// antisymmetric.
    /// </summary>
    /// <remarks>
    /// Equality (local == remote) is impossible in practice — peers cannot
    /// invite themselves — but the method returns <c>false</c> in that
    /// degenerate case so callers don't deadlock waiting for an ACCEPT
    /// from themselves.
    /// </remarks>
    public static bool IsLocalYielder(PeerId local, PeerId remote)
        => string.CompareOrdinal(local.Value, remote.Value) < 0;
}
