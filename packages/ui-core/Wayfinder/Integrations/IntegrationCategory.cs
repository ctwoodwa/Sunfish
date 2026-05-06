using System.Text.Json.Serialization;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Closed integration-category taxonomy per ADR 0067 §3.4. The 6 v1
/// values cover the canonical integration categories supported by the
/// Atlas Integration-Config surface; per-tenant custom categories
/// compose on these via the per-provider <see cref="IntegrationProviderSchema"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntegrationCategory
{
    /// <summary>Payment-processor integration (Stripe / Square / etc.).</summary>
    Payments,

    /// <summary>Transactional email (SendGrid / Postmark / etc.).</summary>
    TransactionalEmail,

    /// <summary>Marketing email / newsletter (Mailchimp / etc.).</summary>
    MarketingEmail,

    /// <summary>SMS / messaging (Twilio / etc.).</summary>
    Messaging,

    /// <summary>Mesh VPN (Tailscale / Headscale / etc.) per ADR 0028 transport.</summary>
    MeshVpn,

    /// <summary>CAPTCHA provider (hCaptcha / reCAPTCHA / etc.).</summary>
    Captcha,
}
