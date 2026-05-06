using System.Collections.Generic;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-provider schema describing the credential fields, display
/// metadata, and help URL for a single integration provider per ADR
/// 0067 §3.1. Schemas are registered via
/// <see cref="IIntegrationSchemaProvider.GetSchemas"/>; one schema per
/// supported provider per category.
/// </summary>
/// <param name="ProviderId">Wire-format identifier (kebab-case; e.g., <c>"stripe"</c>).</param>
/// <param name="DisplayName">Localized display name rendered in the provider-picker.</param>
/// <param name="Category">Discriminator binding the schema to an <see cref="IntegrationCategory"/>.</param>
/// <param name="CredentialFields">Per-credential-field schema list; rendered in registration order.</param>
/// <param name="HelpText">Optional localized help text for the provider as a whole.</param>
/// <param name="DocumentationUrl">Optional URL to provider-specific setup documentation.</param>
public sealed record IntegrationProviderSchema(
    string ProviderId,
    string DisplayName,
    IntegrationCategory Category,
    IReadOnlyList<CredentialFieldSpec> CredentialFields,
    string? HelpText,
    string? DocumentationUrl);
