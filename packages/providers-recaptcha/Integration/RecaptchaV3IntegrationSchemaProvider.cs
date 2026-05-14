using System.Collections.Generic;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Recaptcha.Integration;

/// <summary>
/// <see cref="IIntegrationSchemaProvider"/> for the Google reCAPTCHA v3
/// captcha adapter per ADR 0067 §6.2 / W#48 Phase 3b. Returns the three
/// credential fields that <see cref="RecaptchaV3IntegrationValidator"/>
/// consumes: <c>site-key</c>, <c>secret-key</c>, and the optional
/// <c>verify-endpoint</c> override.
/// </summary>
public sealed class RecaptchaV3IntegrationSchemaProvider : IIntegrationSchemaProvider
{
    private static readonly IReadOnlyList<IntegrationProviderSchema> Schemas =
    [
        new IntegrationProviderSchema(
            ProviderId: "recaptcha-v3",
            DisplayName: "Google reCAPTCHA v3",
            Category: IntegrationCategory.Captcha,
            CredentialFields:
            [
                new CredentialFieldSpec(
                    Key: "site-key",
                    DisplayLabel: "Site Key",
                    Kind: CredentialFieldKind.Text,
                    AutocompleteHint: CredentialAutocompleteHint.None,
                    IsRequired: true,
                    HelpText: "Public reCAPTCHA v3 site key — included in client-side JavaScript.",
                    Placeholder: "6L..."),
                new CredentialFieldSpec(
                    Key: "secret-key",
                    DisplayLabel: "Secret Key",
                    Kind: CredentialFieldKind.Secret,
                    AutocompleteHint: CredentialAutocompleteHint.CurrentPassword,
                    IsRequired: true,
                    HelpText: "Server-side reCAPTCHA v3 secret key used to verify responses.",
                    Placeholder: null),
                new CredentialFieldSpec(
                    Key: "verify-endpoint",
                    DisplayLabel: "Verify Endpoint (optional)",
                    Kind: CredentialFieldKind.Url,
                    AutocompleteHint: CredentialAutocompleteHint.Url,
                    IsRequired: false,
                    HelpText: $"Override Google's default verify URL ({RecaptchaV3Config.DefaultVerifyEndpoint}). Use for on-premise proxies or testing.",
                    Placeholder: RecaptchaV3Config.DefaultVerifyEndpoint),
            ],
            HelpText: "Google reCAPTCHA v3 protects your site from spam and abuse without user interaction.",
            DocumentationUrl: "https://developers.google.com/recaptcha/docs/v3")
    ];

    /// <inheritdoc />
    public IReadOnlyList<IntegrationProviderSchema> GetSchemas() => Schemas;
}
