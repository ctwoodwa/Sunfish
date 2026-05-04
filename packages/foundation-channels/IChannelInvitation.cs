using System.Threading;
using System.Threading.Tasks;
using Sunfish.Federation.Common;

namespace Sunfish.Foundation.Channels;

/// <summary>
/// Inbound channel invitation surfaced by
/// <see cref="IChannelProvider.ListenAsync"/>. Per ADR 0076.
/// </summary>
public interface IChannelInvitation
{
    /// <summary>The peer that sent the INVITE.</summary>
    PeerId FromPeer { get; }

    /// <summary>
    /// Capabilities the inviter offered (flags-combined). Caller inspects
    /// individual bits to decide which capability tier to negotiate.
    /// </summary>
    ChannelCapability OfferedCapabilities { get; }

    /// <summary>
    /// Accept the invitation; returns the active session.
    /// </summary>
    Task<IChannelSession> AcceptAsync(CancellationToken ct);

    /// <summary>
    /// Reject the invitation. <paramref name="reason"/> is propagated to
    /// the inviter for operator-visible feedback.
    /// </summary>
    Task RejectAsync(string? reason, CancellationToken ct);
}
