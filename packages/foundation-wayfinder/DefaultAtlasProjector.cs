using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// In-process implementation of <see cref="IAtlasProjector"/>. Per ADR 0065 §5.
/// </summary>
/// <remarks>
/// <para>
/// Phase 3a substrate: linear scan + LWW resolution. Phase 3b adds
/// schema-registration analyzer + perf benchmarks at 1K / 5K / 10K / 50K
/// settings; production hosts that exceed the Phase-3b cold-projection budget
/// (P95 ≤ 200ms at 10K per ADR 0065 council F9) should swap in a
/// projector backed by a tenant-scoped index.
/// </para>
/// <para>
/// Schema descriptors are registered up-front per scope+path via
/// <see cref="RegisterSchema"/>. Paths without a registered descriptor get a
/// best-effort inferred <see cref="AtlasSettingKind"/> (String / Number /
/// Boolean / JsonObject) from the most recent triple's
/// <see cref="StandingOrderTriple.NewValue"/>.
/// </para>
/// </remarks>
public sealed class DefaultAtlasProjector : IAtlasProjector
{
    private readonly IStandingOrderRepository _repository;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<(StandingOrderScope, string), AtlasSchemaDescriptor> _schemas = new();

    /// <summary>
    /// Construct a projector bound to the supplied repository.
    /// </summary>
    public DefaultAtlasProjector(IStandingOrderRepository repository, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(time);
        _repository = repository;
        _time = time;
    }

    /// <summary>
    /// Register a schema descriptor for a specific (scope, path) pair. Hosts
    /// call this during <c>AddSunfishWayfinder()</c> wiring (Phase 3b's
    /// analyzer enforces that every <c>AddSunfishX()</c> call surface
    /// declares schemas for the paths it expects to settle).
    /// </summary>
    public void RegisterSchema(StandingOrderScope scope, string path, AtlasSchemaDescriptor schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(schema);
        _schemas[(scope, path)] = schema;
    }

    /// <inheritdoc />
    public async ValueTask<AtlasView> ProjectAsync(
        TenantId tenantId,
        StandingOrderScope? scopeFilter,
        CancellationToken ct)
    {
        var winners = new Dictionary<(StandingOrderScope Scope, string Path), (StandingOrder Order, JsonNode? Value)>();

        await foreach (var order in _repository.EnumerateAsync(tenantId, ct).ConfigureAwait(false))
        {
            // Per ADR 0065 §2 + the AtlasView contract: only Validated /
            // Applied orders contribute to the projection. Issued orders
            // (validation pipeline not yet run) and Conflicted / Rejected /
            // Rescinded orders are excluded.
            if (order.State is not (StandingOrderState.Validated or StandingOrderState.Applied))
            {
                continue;
            }
            if (scopeFilter.HasValue && order.Scope != scopeFilter.Value)
            {
                continue;
            }

            foreach (var triple in order.Triples)
            {
                var key = (order.Scope, triple.Path);
                if (!winners.TryGetValue(key, out var current) || OrderWins(order, current.Order))
                {
                    winners[key] = (order, triple.NewValue);
                }
            }
        }

        // ADR 0065 §2 LWW is per (Scope, Path); the dictionary key on
        // AtlasView.SettingsByPath therefore composites scope + path
        // (form: "<scope>:<path>"; lowercase scope name) so two scopes
        // setting the same path produce two distinct snapshots rather
        // than collapsing non-deterministically.
        var settings = new Dictionary<string, AtlasSettingSnapshot>(StringComparer.Ordinal);
        foreach (var ((scope, path), (order, value)) in winners)
        {
            var schema = _schemas.TryGetValue((scope, path), out var registered)
                ? registered
                : InferSchema(path, value);
            var compositeKey = $"{scope.ToString().ToLowerInvariant()}:{path}";
            settings[compositeKey] = new AtlasSettingSnapshot(path, value, order.Id, order.IssuedAt, schema);
        }

        return new AtlasView(tenantId, _time.GetUtcNow(), settings);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AtlasSearchHit> SearchAsync(
        TenantId tenantId,
        string query,
        int limit,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        if (limit <= 0)
        {
            yield break;
        }

        var view = await ProjectAsync(tenantId, scopeFilter: null, ct).ConfigureAwait(false);
        var lowerQuery = query.ToLowerInvariant();

        // Iterate values; the dict key is "<scope>:<path>" composite, but
        // search match + display use the snapshot's real path field.
        var hits = new List<AtlasSearchHit>();
        foreach (var snapshot in view.SettingsByPath.Values)
        {
            var score = ScoreMatch(snapshot.Path, snapshot.Schema.DisplayName, lowerQuery);
            if (score <= 0)
            {
                continue;
            }
            var snippet = MakeSnippet(snapshot.Path, snapshot.Schema.DisplayName, lowerQuery);
            hits.Add(new AtlasSearchHit(snapshot.Path, snapshot.Schema.DisplayName, snippet, score));
        }

        // Stream descending by score (deterministic tiebreak by path).
        var ordered = hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Path, StringComparer.Ordinal)
            .Take(limit);

        foreach (var hit in ordered)
        {
            ct.ThrowIfCancellationRequested();
            yield return hit;
        }
    }

    /// <summary>
    /// LWW resolution per ADR 0065 §2: last-writer-wins-by-IssuedAt-then-IssuedBy.
    /// Tiebreak on equal <see cref="StandingOrder.IssuedAt"/> is the
    /// <see cref="ActorId.Value"/> string under <c>string.CompareOrdinal</c>
    /// — ordinal (not invariant-culture) so the result is identical across
    /// every locale a Sunfish replica might run in.
    /// </summary>
    private static bool OrderWins(StandingOrder candidate, StandingOrder incumbent)
    {
        if (candidate.IssuedAt > incumbent.IssuedAt)
        {
            return true;
        }
        if (candidate.IssuedAt < incumbent.IssuedAt)
        {
            return false;
        }
        return string.CompareOrdinal(candidate.IssuedBy.Value, incumbent.IssuedBy.Value) > 0;
    }

    private static AtlasSchemaDescriptor InferSchema(string path, JsonNode? value)
    {
        // Phase 3a inference — best-effort kind from the value's JSON shape.
        // Phase 3b's analyzer + the registered-schema fast-path replace this.
        // Use the explicit JsonElement.ValueKind discriminator rather than
        // pattern-matching against TryGetValue<bool> — the latter relies on
        // System.Text.Json's undocumented strict-by-kind behaviour, while
        // ValueKind is contract-stable.
        AtlasSettingKind kind;
        if (value is null)
        {
            kind = AtlasSettingKind.String;
        }
        else
        {
            var element = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(value.ToJsonString());
            kind = element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => AtlasSettingKind.Boolean,
                System.Text.Json.JsonValueKind.Number => AtlasSettingKind.Number,
                // Both objects and arrays render via the JsonObject form-view
                // path (see AtlasSettingKind.JsonObject doc-comment — covers
                // any structured JSON; arrays render as JSON tree).
                System.Text.Json.JsonValueKind.Object or System.Text.Json.JsonValueKind.Array => AtlasSettingKind.JsonObject,
                _ => AtlasSettingKind.String,
            };
        }
        var schemaShell = JsonNode.Parse("{}")!;
        return new AtlasSchemaDescriptor(schemaShell, path, "(no descriptor registered)", kind);
    }

    private static double ScoreMatch(string path, string displayName, string lowerQuery)
    {
        var lowerPath = path.ToLowerInvariant();
        var lowerName = displayName.ToLowerInvariant();
        // Exact equality on path: 1.0
        if (lowerPath == lowerQuery) return 1.0;
        // Path prefix match: 0.85
        if (lowerPath.StartsWith(lowerQuery, StringComparison.Ordinal)) return 0.85;
        // Display name prefix match: 0.75
        if (lowerName.StartsWith(lowerQuery, StringComparison.Ordinal)) return 0.75;
        // Path substring match: 0.55
        if (lowerPath.Contains(lowerQuery, StringComparison.Ordinal)) return 0.55;
        // Display name substring match: 0.4
        if (lowerName.Contains(lowerQuery, StringComparison.Ordinal)) return 0.4;
        return 0.0;
    }

    private static string MakeSnippet(string path, string displayName, string lowerQuery)
    {
        // Pick whichever matched (path preferred over display name); return
        // the matching field with surrounding context.
        var lowerPath = path.ToLowerInvariant();
        if (lowerPath.Contains(lowerQuery, StringComparison.Ordinal))
        {
            return path;
        }
        return displayName;
    }
}
