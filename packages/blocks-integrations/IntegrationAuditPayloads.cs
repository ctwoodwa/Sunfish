using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Blocks.Integrations;

/// <summary>
/// Typed audit payload factories for ADR 0067 §8 events. Creates
/// <see cref="AuditPayload"/> bodies with the allowlisted field set;
/// callers sign and append via <see cref="IAuditTrail.AppendAsync"/>.
/// </summary>
/// <remarks>
/// <para><b>Architectural amendment:</b> the hand-off specified this class in
/// <c>packages/ui-core/</c>, but <c>ui-core</c> cannot reference
/// <c>kernel-audit</c> (cycle: <c>kernel-audit → kernel-security → ui-core</c>).
/// The class moves to <c>blocks-integrations</c> per the same cycle-resolution
/// rationale as the Phase 2 addendum for
/// <c>DefaultIntegrationAtlasProvider</c>.</para>
/// <para><b>Redaction rule:</b> ADR 0067 §8 forbids including credential values
/// in any payload. Forbidden field names (case-insensitive, recursive key scan):
/// <c>value</c>, <c>apiKey</c>, <c>secret</c>, <c>password</c>, <c>token</c>,
/// <c>webhookSecret</c>; any key starting with <c>credential.</c> or ending
/// with <c>.value</c>. The factory methods below are the only permitted way
/// to construct audit payloads for ADR 0067 event types — free-form
/// construction is flagged by the <c>SUNFISH_INTEGRATION_AUDIT001</c>
/// Roslyn analyzer.</para>
/// </remarks>
public static class IntegrationAuditPayloads
{
    /// <summary>
    /// Creates the payload body for a provider-change event
    /// (<see cref="AuditEventType.IntegrationProviderChanged"/>).
    /// </summary>
    /// <param name="category">Integration category being changed.</param>
    /// <param name="previousProvider">Prior active provider id; null when none was set.</param>
    /// <param name="newProvider">Newly activated provider id.</param>
    /// <param name="tenantId">Tenant scope.</param>
    public static AuditPayload CreateProviderChangedPayload(
        IntegrationCategory category,
        string? previousProvider,
        string newProvider,
        TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(newProvider);
        return new AuditPayload(new Dictionary<string, object?>
        {
            ["category"] = category.ToString(),
            ["previous_provider"] = previousProvider,
            ["new_provider"] = newProvider,
            ["tenant_id"] = tenantId.Value,
        });
    }

    /// <summary>
    /// Creates the payload body for a credential-update event
    /// (<see cref="AuditEventType.IntegrationCredentialUpdated"/>).
    /// NEVER includes the credential value per §8 redaction rule.
    /// </summary>
    /// <param name="category">Integration category.</param>
    /// <param name="provider">Provider id whose credential changed.</param>
    /// <param name="credentialKey">Name of the credential field that was updated.</param>
    /// <param name="tenantId">Tenant scope.</param>
    public static AuditPayload CreateCredentialUpdatedPayload(
        IntegrationCategory category,
        string provider,
        string credentialKey,
        TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(credentialKey);
        return new AuditPayload(new Dictionary<string, object?>
        {
            ["category"] = category.ToString(),
            ["provider"] = provider,
            ["credential_key"] = credentialKey,
            ["tenant_id"] = tenantId.Value,
            // credential_value intentionally ABSENT per §8 redaction rule
        });
    }

    /// <summary>
    /// Creates the payload body for a validation-succeeded event
    /// (<see cref="AuditEventType.IntegrationValidationSucceeded"/>).
    /// </summary>
    public static AuditPayload CreateValidationSucceededPayload(
        IntegrationCategory category,
        string provider,
        DateTimeOffset validatedAt,
        TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new AuditPayload(new Dictionary<string, object?>
        {
            ["category"] = category.ToString(),
            ["provider"] = provider,
            ["validated_at"] = validatedAt.ToString("O"),
            ["tenant_id"] = tenantId.Value,
        });
    }

    /// <summary>
    /// Creates the payload body for a validation-failed event
    /// (<see cref="AuditEventType.IntegrationValidationFailed"/>).
    /// </summary>
    public static AuditPayload CreateValidationFailedPayload(
        IntegrationCategory category,
        string provider,
        DateTimeOffset validatedAt,
        string errorCode,
        string errorMessage,
        TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(errorCode);
        ArgumentNullException.ThrowIfNull(errorMessage);
        return new AuditPayload(new Dictionary<string, object?>
        {
            ["category"] = category.ToString(),
            ["provider"] = provider,
            ["validated_at"] = validatedAt.ToString("O"),
            ["error_code"] = errorCode,
            ["error_message"] = errorMessage,
            ["tenant_id"] = tenantId.Value,
        });
    }
}
