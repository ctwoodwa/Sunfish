using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Egress payload submitted to an <see cref="IMessagingGateway"/> for
/// dispatch. The gateway returns <see cref="OutboundMessageResult"/> with the
/// provider's message id + initial status.
/// </summary>
public sealed record OutboundMessageRequest
{
    /// <summary>Stable identifier for the message at the substrate level (precedes provider id).</summary>
    public required MessageId Id { get; init; }

    /// <summary>Tenant the message is scoped to; required per <c>IMustHaveTenant</c>.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The thread this message belongs to; routes inbound replies back via the thread token.</summary>
    public required ThreadId Thread { get; init; }

    /// <summary>Transport channel (Email, Sms, ProviderInternal).</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Sending participant (operator on outbound).</summary>
    public required Participant Sender { get; init; }

    /// <summary>Recipients — at least one entry, all reachable via <see cref="Channel"/>.</summary>
    public required IReadOnlyList<Participant> Recipients { get; init; }

    /// <summary>Subject line (Email) or first-line label (SMS); used by some providers as the message preview.</summary>
    public required string Subject { get; init; }

    /// <summary>Plain-text body. HTML body variants are deferred to a Phase 2.x record extension.</summary>
    public required string Body { get; init; }

    /// <summary>Per-message visibility override; defaults to <see cref="MessageVisibility.Public"/> if not narrower.</summary>
    public MessageVisibility Visibility { get; init; } = MessageVisibility.Public;

    /// <summary>Optional thread token to inline in the outbound body / Reply-To header per <see cref="SmsThreadTokenStrategy"/> + ADR 0052 amendment A2; null = gateway decides per provider config.</summary>
    public ThreadToken? Token { get; init; }
}
