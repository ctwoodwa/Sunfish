namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Egress-side contract: dispatches an <see cref="OutboundMessageRequest"/>
/// via a provider adapter (Postmark, SendGrid, Twilio, etc.). Status updates
/// arrive asynchronously through provider webhooks and surface via
/// <see cref="GetStatusAsync"/>.
/// </summary>
/// <remarks>
/// Implementations live in <c>providers-*</c> packages per ADR 0013
/// provider-neutrality enforcement. <c>blocks-messaging</c> references this
/// contract only — never a concrete provider.
/// </remarks>
public interface IMessagingGateway
{
    /// <summary>Dispatches a message and returns the substrate id + provider id + initial status.</summary>
    /// <param name="request">The outbound payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OutboundMessageResult> SendAsync(OutboundMessageRequest request, CancellationToken ct);

    /// <summary>Reads the latest status the gateway has observed for a previously-dispatched provider message id.</summary>
    /// <param name="providerMessageId">Provider-specific id from <see cref="OutboundMessageResult.ProviderMessageId"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OutboundMessageStatus> GetStatusAsync(string providerMessageId, CancellationToken ct);
}
