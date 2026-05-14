using System.Collections.Generic;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Mesh.Headscale.Integration;

/// <summary>
/// <see cref="IIntegrationSchemaProvider"/> for the Headscale mesh-VPN
/// adapter per ADR 0067 §6.2 / W#48 Phase 3b. Returns the three
/// credential fields that <see cref="HeadscaleIntegrationValidator"/>
/// consumes: <c>base-url</c>, <c>api-key</c>, and the optional
/// <c>user</c> namespace.
/// </summary>
public sealed class HeadscaleIntegrationSchemaProvider : IIntegrationSchemaProvider
{
    private static readonly IReadOnlyList<IntegrationProviderSchema> Schemas =
    [
        new IntegrationProviderSchema(
            ProviderId: "headscale",
            DisplayName: "Headscale",
            Category: IntegrationCategory.MeshVpn,
            CredentialFields:
            [
                new CredentialFieldSpec(
                    Key: "base-url",
                    DisplayLabel: "Control-Plane URL",
                    Kind: CredentialFieldKind.Url,
                    AutocompleteHint: CredentialAutocompleteHint.Url,
                    IsRequired: true,
                    HelpText: "Base URL of your Headscale instance (e.g. https://headscale.example.com).",
                    Placeholder: "https://headscale.example.com"),
                new CredentialFieldSpec(
                    Key: "api-key",
                    DisplayLabel: "API Key",
                    Kind: CredentialFieldKind.Secret,
                    AutocompleteHint: CredentialAutocompleteHint.CurrentPassword,
                    IsRequired: true,
                    HelpText: "Headscale API key with node-read access.",
                    Placeholder: null),
                new CredentialFieldSpec(
                    Key: "user",
                    DisplayLabel: "User Namespace (optional)",
                    Kind: CredentialFieldKind.Text,
                    AutocompleteHint: CredentialAutocompleteHint.Username,
                    IsRequired: false,
                    HelpText: "Headscale user namespace for registered nodes. Leave blank to use the API key's default user.",
                    Placeholder: null),
            ],
            HelpText: "Headscale is an open-source, self-hosted WireGuard-based mesh VPN control plane.",
            DocumentationUrl: "https://headscale.net/ref/acls/")
    ];

    /// <inheritdoc />
    public IReadOnlyList<IntegrationProviderSchema> GetSchemas() => Schemas;
}
