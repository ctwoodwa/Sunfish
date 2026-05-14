using System;
using System.Collections.Generic;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Blocks.Integrations.Tests;

/// <summary>
/// ADR 0067 §8 redaction rule: credential values must NEVER appear in audit payloads.
/// Tests the <see cref="IntegrationAuditPayloads"/> factory allowlist.
/// </summary>
public sealed class IntegrationAuditRedactionTests
{
    private static readonly TenantId TenantA = new("t1");

    [Fact]
    public void CreateProviderChangedPayload_AllowedKeys_NoForbiddenKeys()
    {
        var payload = IntegrationAuditPayloads.CreateProviderChangedPayload(
            IntegrationCategory.TransactionalEmail,
            "sendgrid-old",
            "sendgrid-new",
            TenantA);

        AssertNoForbiddenKeys(payload.Body);
        Assert.True(payload.Body.ContainsKey("category"));
        Assert.True(payload.Body.ContainsKey("previous_provider"));
        Assert.True(payload.Body.ContainsKey("new_provider"));
        Assert.True(payload.Body.ContainsKey("tenant_id"));
    }

    [Fact]
    public void CreateCredentialUpdatedPayload_NeverContainsCredentialValue()
    {
        var payload = IntegrationAuditPayloads.CreateCredentialUpdatedPayload(
            IntegrationCategory.TransactionalEmail,
            "sendgrid",
            "apiKey",
            TenantA);

        var json = JsonSerializer.Serialize(payload.Body);

        // The value must not be in the payload — credential_value intentionally absent
        Assert.DoesNotContain("credential_value", json);

        // The key name IS allowed
        Assert.Contains("credential_key", json);
        Assert.Contains("apiKey", json);
        AssertNoForbiddenKeys(payload.Body);
    }

    [Fact]
    public void CreateValidationSucceededPayload_NoForbiddenKeys()
    {
        var payload = IntegrationAuditPayloads.CreateValidationSucceededPayload(
            IntegrationCategory.Payments,
            "stripe",
            DateTimeOffset.UtcNow,
            TenantA);

        AssertNoForbiddenKeys(payload.Body);
    }

    [Fact]
    public void CreateValidationFailedPayload_NoForbiddenKeys()
    {
        var payload = IntegrationAuditPayloads.CreateValidationFailedPayload(
            IntegrationCategory.Payments,
            "stripe",
            DateTimeOffset.UtcNow,
            "auth-failed",
            "Credentials rejected by provider",
            TenantA);

        AssertNoForbiddenKeys(payload.Body);
    }

    [Fact]
    public void PreviousProvider_WithSecretAsValue_IsAllowed()
    {
        // Negative test: key "previous_provider" with value "secret" is ALLOWED —
        // only key names are screened, not values.
        var payload = IntegrationAuditPayloads.CreateProviderChangedPayload(
            IntegrationCategory.TransactionalEmail,
            "secret",   // value of previousProvider — this is OK (not a credential)
            "newprovider",
            TenantA);

        Assert.True(payload.Body.ContainsKey("previous_provider"));
        Assert.Equal("secret", payload.Body["previous_provider"]?.ToString());
        AssertNoForbiddenKeys(payload.Body);
    }

    [Fact]
    public void AllFactoryMethods_ProduceNoForbiddenTopLevelKeys()
    {
        var payloads = new[]
        {
            IntegrationAuditPayloads.CreateProviderChangedPayload(
                IntegrationCategory.TransactionalEmail, "old", "new", TenantA),
            IntegrationAuditPayloads.CreateCredentialUpdatedPayload(
                IntegrationCategory.TransactionalEmail, "sendgrid", "apiKey", TenantA),
            IntegrationAuditPayloads.CreateValidationSucceededPayload(
                IntegrationCategory.TransactionalEmail, "sendgrid", DateTimeOffset.UtcNow, TenantA),
            IntegrationAuditPayloads.CreateValidationFailedPayload(
                IntegrationCategory.TransactionalEmail, "sendgrid", DateTimeOffset.UtcNow, "e", "m", TenantA),
        };

        foreach (var p in payloads)
        {
            AssertNoForbiddenKeys(p.Body);
        }
    }

    private static readonly HashSet<string> ForbiddenKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "value", "apiKey", "secret", "password", "token", "webhookSecret",
    };

    private static void AssertNoForbiddenKeys(IReadOnlyDictionary<string, object?> body)
    {
        foreach (var key in body.Keys)
        {
            var normalizedKey = key.ToLowerInvariant();

            Assert.DoesNotContain(ForbiddenKeyNames, forbidden =>
                string.Equals(key, forbidden, StringComparison.OrdinalIgnoreCase));

            Assert.False(normalizedKey.StartsWith("credential.", StringComparison.Ordinal),
                $"Key '{key}' starts with forbidden 'credential.' prefix.");

            Assert.False(normalizedKey.EndsWith(".value", StringComparison.Ordinal),
                $"Key '{key}' ends with forbidden '.value' suffix.");
        }
    }
}
