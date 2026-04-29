using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Per-tenant per-channel provider config supplied to the messaging substrate
/// at host wire-up. Concrete provider adapters (e.g., Postmark) consume this
/// for credential resolution, sender isolation, abuse-defense thresholds, and
/// thread-token policy.
/// </summary>
public sealed record MessagingProviderConfig
{
    /// <summary>Tenant this configuration applies to.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Channel this configuration covers (Email, Sms, ProviderInternal).</summary>
    public required MessageChannel Channel { get; init; }

    /// <summary>Provider key identifying the adapter (e.g., <c>postmark</c>, <c>sendgrid</c>, <c>twilio</c>).</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Reference to the credential blob the secrets-management adapter resolves at request time; the substrate itself never holds plaintext credentials.</summary>
    public required CredentialsReference Credentials { get; init; }

    /// <summary>Per-tenant outbound sender isolation strategy per ADR 0052 amendment A3.</summary>
    public SenderIsolationMode SenderIsolation { get; init; } = SenderIsolationMode.SharedDomain;

    /// <summary>SMS thread-token strategy per ADR 0052 amendment A4. Phase 2.1 default omits the token.</summary>
    public SmsThreadTokenStrategy SmsThreadToken { get; init; } = SmsThreadTokenStrategy.OmitToken;

    /// <summary>Optional override of <see cref="IThreadTokenIssuer"/>'s default 90-day TTL.</summary>
    public TimeSpan? ThreadTokenTtl { get; init; }

    /// <summary>Per-sender hourly rate limit for inbound messages (Layer 3 of the 5-layer defense per ADR 0052 amendment A1). Default 30/hr per sender per tenant.</summary>
    public int InboundRateLimitPerSenderPerHour { get; init; } = 30;

    /// <summary>Per-tenant hourly rate limit for inbound messages.</summary>
    public int InboundRateLimitPerTenantPerHour { get; init; } = 300;

    /// <summary>Allow-list of sender domains (Email) or country prefixes (SMS) per ADR 0052 amendment A1 Layer 2. Empty = accept all senders but score every inbound (Phase 2.1 default).</summary>
    public IReadOnlyList<string> AllowedSenderDomains { get; init; } = Array.Empty<string>();

    /// <summary>Explicit allow-list of sender addresses (overrides <see cref="AllowedSenderDomains"/> when non-empty).</summary>
    public IReadOnlyList<string> AllowedFromAddresses { get; init; } = Array.Empty<string>();
}
