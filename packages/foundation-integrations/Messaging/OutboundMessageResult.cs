namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Result of an <see cref="IMessagingGateway.SendAsync"/> call. The
/// gateway returns the substrate-side <see cref="MessageId"/> (echoed back
/// from the request) plus a provider-specific id + initial status.
/// </summary>
/// <param name="MessageId">Echo of the substrate-side message id from the request.</param>
/// <param name="ProviderMessageId">Provider-specific message id (e.g., Postmark's MessageID, Twilio's SMS SID); used to correlate later <see cref="OutboundMessageStatus"/> webhooks.</param>
/// <param name="InitialStatus">Status reported by the provider at dispatch time.</param>
public sealed record OutboundMessageResult(
    MessageId MessageId,
    string ProviderMessageId,
    OutboundMessageStatus InitialStatus);

/// <summary>Lifecycle status of an outbound message as reported by the provider.</summary>
public enum OutboundMessageStatus
{
    /// <summary>Accepted by the provider but not yet sent (queued for dispatch).</summary>
    Queued,

    /// <summary>Provider-reported sent (handed to the next hop).</summary>
    Sent,

    /// <summary>Provider-reported delivered to the recipient endpoint.</summary>
    Delivered,

    /// <summary>Soft- or hard-bounced; recipient address rejected.</summary>
    Bounced,

    /// <summary>Recipient flagged as spam/abuse.</summary>
    Complained,

    /// <summary>Recipient opened the message (Email only; provider-dependent).</summary>
    Opened,

    /// <summary>Recipient clicked a link (Email only; provider-dependent).</summary>
    Clicked,

    /// <summary>Send failed at the provider boundary (network error, auth failure, etc.).</summary>
    Failed
}
