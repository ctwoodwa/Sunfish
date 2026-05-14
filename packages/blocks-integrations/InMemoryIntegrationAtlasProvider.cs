using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Blocks.Integrations;

/// <summary>
/// In-memory <see cref="IIntegrationAtlasProvider"/> for consumer package tests per ADR 0067 §7.2.
/// State is held in-process with no encryption, no Standing Orders, and no audit emission.
/// NOT for production use.
/// </summary>
public sealed class InMemoryIntegrationAtlasProvider : IIntegrationAtlasProvider
{
    private readonly Dictionary<IntegrationCategory, ActiveProviderSnapshot?> _activeByCategory = new();
    private readonly Dictionary<IntegrationCategory, ProviderValidationStatus> _statusByCategory = new();
    private readonly Dictionary<IntegrationCategory, List<ProviderValidationStatusEntry>> _history = new();
    private readonly List<IntegrationProviderSchema> _schemas;
    private IntegrationEmailRouting? _routing;

    /// <param name="schemas">Schemas to serve from <see cref="GetSchemas"/>.</param>
    public InMemoryIntegrationAtlasProvider(IEnumerable<IntegrationProviderSchema>? schemas = null)
    {
        _schemas = schemas is not null ? new List<IntegrationProviderSchema>(schemas) : [];
    }

    /// <summary>Sets the active provider snapshot for a category (test helper).</summary>
    public void SetActiveProvider(IntegrationCategory category, ActiveProviderSnapshot? snapshot)
        => _activeByCategory[category] = snapshot;

    /// <summary>Sets the validation status for a category (test helper).</summary>
    public void SetValidationStatus(IntegrationCategory category, ProviderValidationStatus status)
        => _statusByCategory[category] = status;

    /// <summary>Sets the email routing (test helper).</summary>
    public void SetRouting(IntegrationEmailRouting? routing) => _routing = routing;

    /// <summary>Last Standing Order id issued (test verification).</summary>
    public StandingOrderId? LastIssuedOrderId { get; private set; }

    /// <summary>Last sensitive credential update (category, key) pair — test verification.</summary>
    public (IntegrationCategory Category, string Key)? LastSensitiveCredentialUpdate { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IntegrationProviderSchema> GetSchemas() => _schemas;

    /// <inheritdoc />
    public Task<IntegrationAtlasView> GetAtlasViewAsync(CancellationToken ct = default)
    {
        var activeByCategory = new Dictionary<IntegrationCategory, ActiveProviderSnapshot?>(_activeByCategory);
        var statusByCategory = new Dictionary<IntegrationCategory, ProviderValidationStatus>(_statusByCategory);
        var credentialsByProvider = new Dictionary<IntegrationCategory, IReadOnlyList<ProviderValidationStatusEntry>>();
        foreach (var (cat, hist) in _history)
        {
            credentialsByProvider[cat] = hist.AsReadOnly();
        }
        return Task.FromResult(new IntegrationAtlasView(activeByCategory, statusByCategory, credentialsByProvider, _routing));
    }

    /// <inheritdoc />
    public Task<StandingOrderId> IssueProviderChangeAsync(
        IntegrationCategory category, string providerId, IIntegrationAtlasContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        _activeByCategory[category] = new ActiveProviderSnapshot(
            providerId, DateTimeOffset.UtcNow, ctx.CurrentActorId, default);
        LastIssuedOrderId = new StandingOrderId(Guid.NewGuid());
        return Task.FromResult(LastIssuedOrderId.Value);
    }

    /// <inheritdoc />
    public Task<StandingOrderId> IssueSensitiveCredentialAsync(
        IntegrationCategory category, string providerId, string credentialKey,
        ReadOnlyMemory<byte> plaintextBytes, IIntegrationAtlasContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentialKey);
        LastSensitiveCredentialUpdate = (category, credentialKey);
        LastIssuedOrderId = new StandingOrderId(Guid.NewGuid());
        return Task.FromResult(LastIssuedOrderId.Value);
    }

    /// <inheritdoc />
    public Task<StandingOrderId> IssueNonSensitiveCredentialAsync(
        IntegrationCategory category, string providerId, string credentialKey,
        JsonNode value, IIntegrationAtlasContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(credentialKey);
        LastIssuedOrderId = new StandingOrderId(Guid.NewGuid());
        return Task.FromResult(LastIssuedOrderId.Value);
    }

    /// <inheritdoc />
    public Task<IntegrationValidationResult> ValidateProviderAsync(
        IntegrationCategory category, IIntegrationAtlasContext ctx, CancellationToken ct = default)
    {
        var result = new IntegrationValidationResult(
            ProviderValidationStatus.Valid, DateTimeOffset.UtcNow, null, null);
        _statusByCategory[category] = ProviderValidationStatus.Valid;
        if (!_history.TryGetValue(category, out var hist))
        {
            hist = new List<ProviderValidationStatusEntry>();
            _history[category] = hist;
        }
        hist.Add(new ProviderValidationStatusEntry(
            ctx.CurrentTenantId, category, string.Empty, result, ctx.CurrentActorId, DateTimeOffset.UtcNow));
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<StandingOrderId> IssueRoutingAsync(
        IntegrationEmailRouting routing, IIntegrationAtlasContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(routing);
        _routing = routing;
        LastIssuedOrderId = new StandingOrderId(Guid.NewGuid());
        return Task.FromResult(LastIssuedOrderId.Value);
    }
}
