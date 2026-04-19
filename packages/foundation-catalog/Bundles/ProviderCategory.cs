using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>Categories of external providers a bundle may require or optionally support.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderCategory
{
    /// <summary>Subscription / usage billing engines.</summary>
    Billing = 0,

    /// <summary>Payment gateways / card acquirers.</summary>
    Payments = 1,

    /// <summary>Bank transaction / ACH import feeds.</summary>
    BankingFeed = 2,

    /// <summary>Feature-flag / experimentation providers.</summary>
    FeatureFlags = 3,

    /// <summary>Short-term-rental / OTA channel managers.</summary>
    ChannelManager = 4,

    /// <summary>Email / SMS / chat messaging providers.</summary>
    Messaging = 5,

    /// <summary>Blob / object storage backends.</summary>
    Storage = 6,

    /// <summary>Identity providers (OIDC / SAML).</summary>
    IdentityProvider = 7,

    /// <summary>Other provider categories not otherwise enumerated.</summary>
    Other = 99,
}
