using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>Deployment modes a bundle declares support for.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeploymentMode
{
    /// <summary>Local-first / offline-capable lite mode.</summary>
    Lite = 0,

    /// <summary>Tenant-controlled self-hosted deployment.</summary>
    SelfHosted = 1,

    /// <summary>Bridge-managed hosted SaaS.</summary>
    HostedSaaS = 2,
}
