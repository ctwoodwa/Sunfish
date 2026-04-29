namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>Direction of a message relative to the platform.</summary>
public enum MessageDirection
{
    /// <summary>Received from an external sender (provider webhook ingress).</summary>
    Inbound,

    /// <summary>Sent to an external recipient (provider gateway egress).</summary>
    Outbound
}

/// <summary>Transport channel a message rides on.</summary>
public enum MessageChannel
{
    /// <summary>SMTP / IMAP via a provider adapter (Postmark, SendGrid, SES, etc.).</summary>
    Email,

    /// <summary>Mobile SMS / MMS via a provider adapter (Twilio, etc.).</summary>
    Sms,

    /// <summary>Provider-internal channel (e.g., test fixtures, in-memory simulator); never reaches an external network.</summary>
    ProviderInternal
}

/// <summary>
/// Three-tier message visibility per ADR 0052 Minor amendment. Composes with
/// the per-thread participant set + the macaroon-capability projection from
/// ADR 0032 to compute who actually sees what.
/// </summary>
public enum MessageVisibility
{
    /// <summary>Visible to every participant on the thread.</summary>
    Public,

    /// <summary>Visible only to a specific party pair (e.g., owner + vendor private aside on a shared work-order thread).</summary>
    PartyPair,

    /// <summary>Visible only to the operator role (BDFL/property owner); not exposed to vendors, tenants, or applicants.</summary>
    OperatorOnly
}

/// <summary>
/// Per-tenant outbound sender isolation strategy for email per ADR 0052
/// amendment A3. SMS delivery uses a vendor-managed pool and is not affected
/// by this setting.
/// </summary>
public enum SenderIsolationMode
{
    /// <summary>All tenants share <c>messages.bridge.sunfish.dev</c>; reputation pooled.</summary>
    SharedDomain,

    /// <summary>Per-tenant Postmark message-stream identity; same shared domain but provider-tagged for separate reputation.</summary>
    PerTenantStream,

    /// <summary>Per-tenant subdomain with bring-your-own-DKIM/SPF/DMARC. Phase 2.3+; spec hook only in Phase 2.1.</summary>
    PerTenantSubdomain
}

/// <summary>
/// Whether to inline a thread token in outbound SMS bodies per ADR 0052
/// amendment A4. SMS providers strip arbitrary headers; the token has to live
/// in the message body when used.
/// </summary>
public enum SmsThreadTokenStrategy
{
    /// <summary>Phase 2.1 default. Outbound SMS carries no token; inbound replies route via fuzzy sender-recency matching (14-day window).</summary>
    OmitToken,

    /// <summary>First line of the outbound SMS body contains the token (e.g., <c>[Ref: 7A2K…]</c>); inbound replies use the token when preserved by the carrier and fall through to fuzzy matching otherwise.</summary>
    InlineToken
}
