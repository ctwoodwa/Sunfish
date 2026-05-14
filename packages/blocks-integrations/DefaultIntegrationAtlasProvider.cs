using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;
using AuditRecord = Sunfish.Kernel.Audit.AuditRecord;

namespace Sunfish.Blocks.Integrations;

/// <summary>
/// Reference implementation of <see cref="IIntegrationAtlasProvider"/> per ADR 0067 §7.1.
/// Persists integration config as Standing Orders under
/// <see cref="StandingOrderScope.Integration"/>; projects the LWW-merged view via
/// <see cref="IAtlasProjector"/>.
/// </summary>
/// <remarks>
/// <para><b>Encrypt-before-issue invariant:</b>
/// <see cref="IssueSensitiveCredentialAsync"/> calls
/// <see cref="IFieldEncryptor.EncryptAsync"/> BEFORE
/// <see cref="IStandingOrderIssuer.IssueAsync"/>. Verified by
/// <c>SensitiveCredential_IsEncryptedBeforeStandingOrder</c> unit test.</para>
/// <para><b>Fail-closed on capability:</b>
/// <see cref="ValidateProviderAsync"/> returns
/// <see cref="ProviderValidationStatus.Unknown"/> + error code
/// <c>"no-decrypt-capability"</c> when
/// <see cref="IDecryptCapabilityProvider.AcquireAsync"/> returns null.
/// Never throws for capability denial.</para>
/// <para><b>Audit redaction:</b> credential bytes NEVER appear in any
/// <see cref="AuditRecord"/> payload per ADR 0067 §8. Payload factories in
/// <see cref="IntegrationAuditPayloads"/> enforce the allowlist.</para>
/// </remarks>
public sealed class DefaultIntegrationAtlasProvider : IIntegrationAtlasProvider
{
    // Atlas composite key scope prefix per DefaultAtlasProjector convention:
    //   "{scope.ToLower()}:{path}"
    private const string ScopePrefix = "integration";
    private const string PathPrefix = "integration";
    private const string ActiveProviderField = "activeProvider";
    private const string ActivatedByField = "activatedBy";
    private const string ActivatedAtField = "activatedAt";
    private const string ActivationOrderIdField = "activationOrderId";
    private const string RoutingField = "routing";
    private const string CredentialEncryptedSuffix = ".encrypted";

    private static readonly TimeSpan ValidationCapabilityTtl = TimeSpan.FromMinutes(5);

    private readonly IStandingOrderIssuer _issuer;
    private readonly IAtlasProjector _projector;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly IFieldEncryptor _encryptor;
    private readonly IFieldDecryptor _decryptor;
    private readonly IDecryptCapabilityProvider _capabilityProvider;
    private readonly IValidationStatusStore _statusStore;
    private readonly IIntegrationAtlasContext _context;
    private readonly IReadOnlyList<IntegrationProviderSchema> _schemas;
    private readonly IReadOnlyDictionary<(IntegrationCategory, string), IIntegrationProviderValidator> _validators;

    /// <summary>
    /// Initializes a new instance. Throws
    /// <see cref="DuplicateValidatorRegistrationException"/> when two validators share the
    /// same <c>(SupportedCategory, SupportedProvider)</c> pair per ADR 0067 §6.2.1.
    /// </summary>
    /// <remarks>
    /// <b>Architectural amendment (Phase 2 addendum):</b> the hand-off did not list
    /// <see cref="IOperationSigner"/> as a constructor dep, but <see cref="IAuditTrail.AppendAsync"/>
    /// requires a <see cref="SignedOperation{T}"/> envelope — signing is mandatory. Added per
    /// cohort precedent (<c>DefaultAlertRouter</c>, <c>ShipsOfficeCommandService</c>).
    /// </remarks>
    public DefaultIntegrationAtlasProvider(
        IStandingOrderIssuer issuer,
        IAtlasProjector projector,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        IFieldEncryptor encryptor,
        IFieldDecryptor decryptor,
        IDecryptCapabilityProvider capabilityProvider,
        IValidationStatusStore statusStore,
        IIntegrationAtlasContext context,
        IEnumerable<IIntegrationSchemaProvider> schemaProviders,
        IEnumerable<IIntegrationProviderValidator> validators)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(encryptor);
        ArgumentNullException.ThrowIfNull(decryptor);
        ArgumentNullException.ThrowIfNull(capabilityProvider);
        ArgumentNullException.ThrowIfNull(statusStore);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(schemaProviders);
        ArgumentNullException.ThrowIfNull(validators);

        _issuer = issuer;
        _projector = projector;
        _auditTrail = auditTrail;
        _signer = signer;
        _encryptor = encryptor;
        _decryptor = decryptor;
        _capabilityProvider = capabilityProvider;
        _statusStore = statusStore;
        _context = context;
        _schemas = schemaProviders.SelectMany(p => p.GetSchemas()).ToList();

        var validatorMap = new Dictionary<(IntegrationCategory, string), IIntegrationProviderValidator>();
        foreach (var v in validators)
        {
            var key = (v.SupportedCategory, v.SupportedProvider);
            if (!validatorMap.TryAdd(key, v))
            {
                throw new DuplicateValidatorRegistrationException(v.SupportedCategory, v.SupportedProvider);
            }
        }
        _validators = validatorMap;
    }

    /// <inheritdoc />
    public IReadOnlyList<IntegrationProviderSchema> GetSchemas() => _schemas;

    /// <inheritdoc />
    public async Task<IntegrationAtlasView> GetAtlasViewAsync(CancellationToken ct = default)
    {
        var tenantId = _context.CurrentTenantId;
        var atlas = await _projector.ProjectAsync(tenantId, StandingOrderScope.Integration, ct)
            .ConfigureAwait(false);

        var activeByCategory = new Dictionary<IntegrationCategory, ActiveProviderSnapshot?>();
        var statusByCategory = new Dictionary<IntegrationCategory, ProviderValidationStatus>();
        var credentialsByProvider = new Dictionary<IntegrationCategory, IReadOnlyList<ProviderValidationStatusEntry>>();
        IntegrationEmailRouting? routing = null;

        foreach (var category in Enum.GetValues<IntegrationCategory>())
        {
            var categoryPath = $"{PathPrefix}/{category}";

            var providerSnapshot = GetSnapshot(atlas, $"{categoryPath}/{ActiveProviderField}");
            if (providerSnapshot?.CurrentValue?.GetValue<string>() is { } providerId)
            {
                var activatedByStr = GetSnapshot(atlas, $"{categoryPath}/{ActivatedByField}")?.CurrentValue?.GetValue<string>();
                var activatedBy = activatedByStr is not null ? new ActorId(activatedByStr) : default;

                var activatedAtStr = GetSnapshot(atlas, $"{categoryPath}/{ActivatedAtField}")?.CurrentValue?.GetValue<string>();
                var activatedAt = activatedAtStr is not null && DateTimeOffset.TryParse(activatedAtStr, out var dt)
                    ? dt : DateTimeOffset.UtcNow;

                var orderIdStr = GetSnapshot(atlas, $"{categoryPath}/{ActivationOrderIdField}")?.CurrentValue?.GetValue<string>();
                var orderId = orderIdStr is not null && Guid.TryParse(orderIdStr, out var orderGuid)
                    ? new StandingOrderId(orderGuid) : default;

                activeByCategory[category] = new ActiveProviderSnapshot(providerId, activatedAt, activatedBy, orderId);
            }

            var current = await _statusStore.GetCurrentAsync(tenantId, category, string.Empty, ct)
                .ConfigureAwait(false);
            statusByCategory[category] = current?.Result.Status ?? ProviderValidationStatus.Unknown;

            var history = new List<ProviderValidationStatusEntry>();
            await foreach (var entry in _statusStore.HistoryAsync(tenantId, category, string.Empty, 20, ct)
                .ConfigureAwait(false))
            {
                history.Add(entry);
            }
            credentialsByProvider[category] = history;
        }

        var routingSnapshot = GetSnapshot(atlas, $"{PathPrefix}/{RoutingField}");
        if (routingSnapshot?.CurrentValue is not null)
        {
            routing = routingSnapshot.CurrentValue.Deserialize<IntegrationEmailRouting>();
        }

        return new IntegrationAtlasView(activeByCategory, statusByCategory, credentialsByProvider, routing);
    }

    /// <inheritdoc />
    public async Task<StandingOrderId> IssueProviderChangeAsync(
        IntegrationCategory category,
        string providerId,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        var tenantId = ctx.CurrentTenantId;
        var actor = ctx.CurrentActorId;
        var categoryPath = $"{PathPrefix}/{category}";
        var now = DateTimeOffset.UtcNow;

        var triples = new StandingOrderTriple[]
        {
            new($"{categoryPath}/{ActiveProviderField}", null, JsonValue.Create(providerId)),
            new($"{categoryPath}/{ActivatedByField}", null, JsonValue.Create(actor.ToString())),
            new($"{categoryPath}/{ActivatedAtField}", null, JsonValue.Create(now.ToString("O"))),
        };

        var draft = new StandingOrderDraft(
            TenantId: tenantId,
            Scope: StandingOrderScope.Integration,
            Triples: triples,
            Rationale: $"Provider change: {category} → {providerId}",
            ApprovalChain: null);

        var order = await _issuer.IssueAsync(draft, actor, _auditTrail, ct).ConfigureAwait(false);

        await AppendAuditAsync(
            AuditEventType.IntegrationProviderChanged,
            IntegrationAuditPayloads.CreateProviderChangedPayload(category, null, providerId, tenantId),
            tenantId, ct).ConfigureAwait(false);

        return order.Id;
    }

    /// <inheritdoc />
    public async Task<StandingOrderId> IssueSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerId,
        string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(credentialKey);
        var tenantId = ctx.CurrentTenantId;
        var actor = ctx.CurrentActorId;

        // ENCRYPT BEFORE ISSUE — ordering invariant per ADR 0067 §7.1.
        // Verified by SensitiveCredential_IsEncryptedBeforeStandingOrder unit test.
        var encrypted = await _encryptor.EncryptAsync(plaintextBytes, tenantId, ct).ConfigureAwait(false);

        var path = $"{PathPrefix}/{category}/credential/{credentialKey}{CredentialEncryptedSuffix}";
        var encryptedJson = JsonSerializer.SerializeToNode(encrypted);

        var draft = new StandingOrderDraft(
            TenantId: tenantId,
            Scope: StandingOrderScope.Integration,
            Triples: [new StandingOrderTriple(path, null, encryptedJson)],
            Rationale: $"Sensitive credential update: {category}/{providerId}/{credentialKey}",
            ApprovalChain: null);

        var order = await _issuer.IssueAsync(draft, actor, _auditTrail, ct).ConfigureAwait(false);
        await AppendAuditAsync(
            AuditEventType.IntegrationCredentialUpdated,
            IntegrationAuditPayloads.CreateCredentialUpdatedPayload(category, providerId, credentialKey, tenantId),
            tenantId, ct).ConfigureAwait(false);

        return order.Id;
    }

    /// <inheritdoc />
    public async Task<StandingOrderId> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category,
        string providerId,
        string credentialKey,
        JsonNode value,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(credentialKey);
        ArgumentNullException.ThrowIfNull(value);
        var tenantId = ctx.CurrentTenantId;
        var actor = ctx.CurrentActorId;

        var path = $"{PathPrefix}/{category}/credential/{credentialKey}";
        var draft = new StandingOrderDraft(
            TenantId: tenantId,
            Scope: StandingOrderScope.Integration,
            Triples: [new StandingOrderTriple(path, null, value)],
            Rationale: $"Non-sensitive credential update: {category}/{providerId}/{credentialKey}",
            ApprovalChain: null);

        var order = await _issuer.IssueAsync(draft, actor, _auditTrail, ct).ConfigureAwait(false);
        await AppendAuditAsync(
            AuditEventType.IntegrationCredentialUpdated,
            IntegrationAuditPayloads.CreateCredentialUpdatedPayload(category, providerId, credentialKey, tenantId),
            tenantId, ct).ConfigureAwait(false);

        return order.Id;
    }

    /// <inheritdoc />
    public async Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.CurrentTenantId;
        var actor = ctx.CurrentActorId;

        // Step 1: acquire decrypt capability — fail-closed per ADR 0067 §5.3.1
        IDecryptCapability? capability = null;
        try
        {
            capability = await _capabilityProvider
                .AcquireAsync(tenantId, IntegrationCapabilityPurposes.IntegrationValidation, ValidationCapabilityTtl, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* treat acquisition exceptions as "no capability" — fail-closed */ }

        if (capability is null)
        {
            var failResult = new IntegrationValidationResult(
                ProviderValidationStatus.Unknown,
                DateTimeOffset.UtcNow,
                "no-decrypt-capability",
                "Decrypt capability could not be acquired for tenant.");
            await RecordValidationResultAsync(tenantId, category, string.Empty, actor, failResult, ct)
                .ConfigureAwait(false);
            return failResult;
        }

        // Step 2: look up the active provider
        var atlas = await _projector.ProjectAsync(tenantId, StandingOrderScope.Integration, ct)
            .ConfigureAwait(false);
        var providerSnapshot = GetSnapshot(atlas, $"{PathPrefix}/{category}/{ActiveProviderField}");
        if (providerSnapshot?.CurrentValue?.GetValue<string>() is not { } providerId)
        {
            var result = new IntegrationValidationResult(
                ProviderValidationStatus.Unknown, DateTimeOffset.UtcNow,
                "no-validator-registered", $"No active provider configured for category {category}.");
            await RecordValidationResultAsync(tenantId, category, string.Empty, actor, result, ct)
                .ConfigureAwait(false);
            return result;
        }

        // Step 3: look up validator — missing = Unknown + "no-validator-registered"
        if (!_validators.TryGetValue((category, providerId), out var validator))
        {
            var result = new IntegrationValidationResult(
                ProviderValidationStatus.Unknown, DateTimeOffset.UtcNow,
                "no-validator-registered", $"No validator registered for ({category}, {providerId}).");
            await RecordValidationResultAsync(tenantId, category, providerId, actor, result, ct)
                .ConfigureAwait(false);
            return result;
        }

        // Step 4: gather credentials, decrypt sensitive ones, validate, zero buffers in finally
        var sensitiveCredentials = new Dictionary<string, ReadOnlyMemory<byte>>();
        var nonSensitiveCredentials = new Dictionary<string, JsonNode>();
        var credPrefix = $"{PathPrefix}/{category}/credential/";

        foreach (var (compositeKey, snapshot) in atlas.SettingsByPath)
        {
            // composite key is "integration:{path}" — extract the raw path
            var path = compositeKey.StartsWith($"{ScopePrefix}:", StringComparison.Ordinal)
                ? compositeKey[$"{ScopePrefix}:".Length..]
                : compositeKey;

            if (!path.StartsWith(credPrefix, StringComparison.Ordinal)) continue;
            var key = path[credPrefix.Length..];
            var valueNode = snapshot.CurrentValue;

            if (key.EndsWith(CredentialEncryptedSuffix, StringComparison.Ordinal))
            {
                var bareKey = key[..^CredentialEncryptedSuffix.Length];
                if (valueNode is not null)
                {
                    EncryptedField encryptedField;
                    ReadOnlyMemory<byte> plaintext;
                    try
                    {
                        encryptedField = valueNode.Deserialize<EncryptedField>();
                        plaintext = await _decryptor.DecryptAsync(encryptedField, capability, tenantId, ct)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        // Fail-closed: a credential we know about that we cannot decrypt must not
                        // result in validation running against a partial credential set — that would
                        // be a fail-open variant. Return Unknown so the caller retries after the
                        // underlying key-management issue is resolved.
                        var decryptFailResult = new IntegrationValidationResult(
                            ProviderValidationStatus.Unknown,
                            DateTimeOffset.UtcNow,
                            "decrypt-failed",
                            $"Failed to decrypt credential '{bareKey}' for ({category}, {providerId}).");
                        await RecordValidationResultAsync(tenantId, category, providerId, actor, decryptFailResult, ct)
                            .ConfigureAwait(false);
                        // Zero any buffers collected so far before returning
                        foreach (var kvp in sensitiveCredentials)
                        {
                            if (MemoryMarshal.TryGetArray(kvp.Value, out var seg) && seg.Array is not null)
                                CryptographicOperations.ZeroMemory(seg.Array.AsSpan(seg.Offset, seg.Count));
                        }
                        sensitiveCredentials.Clear();
                        return decryptFailResult;
                    }
                    sensitiveCredentials[bareKey] = plaintext;
                }
            }
            else if (valueNode is not null)
            {
                nonSensitiveCredentials[key] = valueNode;
            }
        }

        IntegrationValidationResult validationResult;
        try
        {
            validationResult = await validator.ValidateAsync(sensitiveCredentials, nonSensitiveCredentials, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            // Zero sensitive plaintext buffers per ADR 0067 §7.1 + §5.3.1
            foreach (var kvp in sensitiveCredentials)
            {
                if (MemoryMarshal.TryGetArray(kvp.Value, out var segment) && segment.Array is not null)
                {
                    CryptographicOperations.ZeroMemory(segment.Array.AsSpan(segment.Offset, segment.Count));
                }
            }
            sensitiveCredentials.Clear();
        }

        await RecordValidationResultAsync(tenantId, category, providerId, actor, validationResult, ct)
            .ConfigureAwait(false);
        return validationResult;
    }

    /// <inheritdoc />
    public async Task<StandingOrderId> IssueRoutingAsync(
        IntegrationEmailRouting routing,
        IIntegrationAtlasContext ctx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routing);
        var tenantId = ctx.CurrentTenantId;
        var actor = ctx.CurrentActorId;

        var routingJson = JsonSerializer.SerializeToNode(routing);
        var draft = new StandingOrderDraft(
            TenantId: tenantId,
            Scope: StandingOrderScope.Integration,
            Triples: [new StandingOrderTriple($"{PathPrefix}/{RoutingField}", null, routingJson)],
            Rationale: "Email routing update",
            ApprovalChain: null);

        var order = await _issuer.IssueAsync(draft, actor, _auditTrail, ct).ConfigureAwait(false);
        return order.Id;
    }

    private static AtlasSettingSnapshot? GetSnapshot(AtlasView atlas, string path)
    {
        var compositeKey = $"{ScopePrefix}:{path}";
        return atlas.SettingsByPath.TryGetValue(compositeKey, out var snapshot) ? snapshot : null;
    }

    private async Task RecordValidationResultAsync(
        TenantId tenantId,
        IntegrationCategory category,
        string providerId,
        ActorId actor,
        IntegrationValidationResult result,
        CancellationToken ct)
    {
        await _statusStore.UpdateAsync(tenantId, category, providerId, result, actor, ct)
            .ConfigureAwait(false);

        if (result.Status == ProviderValidationStatus.Valid)
        {
            await AppendAuditAsync(
                AuditEventType.IntegrationValidationSucceeded,
                IntegrationAuditPayloads.CreateValidationSucceededPayload(
                    category, providerId, result.ValidatedAt, tenantId),
                tenantId, ct).ConfigureAwait(false);
        }
        else
        {
            await AppendAuditAsync(
                AuditEventType.IntegrationValidationFailed,
                IntegrationAuditPayloads.CreateValidationFailedPayload(
                    category, providerId, result.ValidatedAt,
                    result.ErrorCode ?? "unknown", result.ErrorMessage ?? string.Empty, tenantId),
                tenantId, ct).ConfigureAwait(false);
        }
    }

    private async Task AppendAuditAsync(
        AuditEventType eventType,
        AuditPayload payload,
        TenantId tenantId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var signed = await _signer.SignAsync(payload, now, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: now,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
