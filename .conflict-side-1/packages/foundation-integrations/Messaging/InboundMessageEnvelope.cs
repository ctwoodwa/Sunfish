namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Normalized inbound payload produced by a provider adapter from a webhook
/// delivery; consumed by the 5-layer defense pipeline before being persisted
/// as a <c>Message</c> (the message entity ships in <c>blocks-messaging</c>
/// per W#20 Phase 2).
/// </summary>
public sealed record InboundMessageEnvelope
{
    /// <summary>Provider key from <see cref="MessagingProviderConfig.ProviderKey"/> identifying which adapter parsed this envelope.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Transport channel that delivered the inbound message.</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Raw provider headers preserved for audit + signature verification.</summary>
    public required IReadOnlyDictionary<string, string> ProviderHeaders { get; init; }

    /// <summary>Raw body bytes preserved for audit + signature verification; the parsed body lives in <see cref="ParsedBody"/>.</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }

    /// <summary>Parsed plain-text body extracted by the provider adapter.</summary>
    public required string ParsedBody { get; init; }

    /// <summary>Subject line / SMS first-line label parsed by the adapter.</summary>
    public required string Subject { get; init; }

    /// <summary>Sender's address (email or E.164 phone) parsed from the inbound payload.</summary>
    public required string SenderAddress { get; init; }

    /// <summary>Optional sender display name (Email only) parsed from the From header.</summary>
    public string? SenderDisplayName { get; init; }

    /// <summary>Thread token extracted from the inbound payload (Reply-To header for Email; first-line reserved label for SMS); null when no token was present and the inbound message will be routed via fuzzy sender-recency matching or queued for unrouted triage.</summary>
    public ThreadToken? ParsedToken { get; init; }

    /// <summary>Wall-clock time the provider accepted the inbound message.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
