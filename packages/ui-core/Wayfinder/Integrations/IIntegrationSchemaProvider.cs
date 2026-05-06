using System.Collections.Generic;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Source of <see cref="IntegrationProviderSchema"/> entries per ADR
/// 0067 §6.2. Adapter packages (e.g., <c>compat-stripe</c>,
/// <c>compat-twilio</c>) implement this to register their per-provider
/// credential schemas; the surface composes the union across all
/// registered providers.
/// </summary>
public interface IIntegrationSchemaProvider
{
    /// <summary>Returns every schema this provider registers. Empty list permitted.</summary>
    IReadOnlyList<IntegrationProviderSchema> GetSchemas();
}
