using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Messaging;

namespace Sunfish.Blocks.Messaging.Models;

/// <summary>
/// A single durable message on a <see cref="Thread"/>. Inbound messages are
/// produced by the 5-layer defense pipeline from an
/// <see cref="InboundMessageEnvelope"/>; outbound messages are produced by
/// <see cref="IMessagingGateway.SendAsync"/>.
/// </summary>
public sealed record Message
{
    /// <summary>Stable identifier; substrate-side, distinct from any provider id.</summary>
    public required MessageId Id { get; init; }

    /// <summary>Thread this message belongs to.</summary>
    public required ThreadId Thread { get; init; }

    /// <summary>Owning tenant; required per <c>IMustHaveTenant</c>.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Whether this message was sent (Outbound) or received (Inbound).</summary>
    public required MessageDirection Direction { get; init; }

    /// <summary>Channel the message rode on.</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Sender — operator (Outbound) or external party (Inbound).</summary>
    public required Participant Sender { get; init; }

    /// <summary>Subject line (Email) or first-line label (SMS).</summary>
    public required string Subject { get; init; }

    /// <summary>Plain-text body. HTML body variants are deferred to a Phase 2.x record extension.</summary>
    public required string Body { get; init; }

    /// <summary>Wall-clock time the message was sent (Outbound) or received (Inbound).</summary>
    public required DateTimeOffset SentOrReceivedAt { get; init; }

    /// <summary>Per-message visibility; defaults to <see cref="MessageVisibility.Public"/> when not narrowed below the thread default.</summary>
    public required MessageVisibility Visibility { get; init; }

    /// <summary>Provider-specific metadata captured at the substrate boundary (e.g., Postmark MessageID, Twilio SMS SID, Reply-To header). Treated as opaque by the substrate.</summary>
    public IReadOnlyDictionary<string, string> ProviderMetadata { get; init; } = new Dictionary<string, string>();
}
