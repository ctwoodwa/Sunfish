using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.MissionSpace.Audit;
using Sunfish.Foundation.Transport;
using Sunfish.Foundation.UI;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Reference <see cref="IMinimumSpecResolver"/> per ADR 0063-A1.1 + A1.7 + A1.8.
/// Pure-function evaluation of the merged spec against a
/// <see cref="MissionEnvelope"/>; no persistent state beyond the
/// per-spec cache.
/// </summary>
public sealed class DefaultMinimumSpecResolver : IMinimumSpecResolver
{
    /// <summary>Default cache TTL per A1.6.</summary>
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>Per A1.7 dedup pattern — 5-minute window for <c>MinimumSpecEvaluated</c>.</summary>
    public static readonly TimeSpan EvaluatedDedupWindow = TimeSpan.FromMinutes(5);

    /// <summary>Per A1.7 dedup pattern — 24-hour window for <c>PostInstallSpecRegression</c>.</summary>
    public static readonly TimeSpan RegressionDedupWindow = TimeSpan.FromHours(24);

    private readonly TimeProvider _time;
    private readonly TimeSpan _cacheTtl;
    private readonly ConcurrentDictionary<CacheKey, CacheEntry> _cache = new();
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _evaluatedDedup = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _regressionDedup = new();
    private readonly ConcurrentDictionary<(MinimumSpec Spec, string? Platform, DimensionChangeKind Dimension), DimensionPassFail> _previousOutcomes = new();

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultMinimumSpecResolver(TimeProvider? time = null, TimeSpan? cacheTtl = null)
    {
        _time = time ?? TimeProvider.System;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultMinimumSpecResolver(
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? cacheTtl = null)
        : this(time, cacheTtl)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
    }

    /// <inheritdoc />
    public async ValueTask<SystemRequirementsResult> EvaluateAsync(
        MinimumSpec spec,
        MissionEnvelope envelope,
        string? platform = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        var key = new CacheKey(spec, envelope.EnvelopeHash, platform);
        var now = _time.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && now - cached.At < _cacheTtl)
        {
            return cached.Result;
        }

        var merged = ComposeWithPlatform(spec, platform);
        var evaluations = new List<DimensionEvaluation>(10);

        Evaluate(DimensionChangeKind.Hardware, merged.Hardware, spec.Policy, evaluations,
            sp => HardwareEvaluation(sp, envelope.Hardware));
        Evaluate(DimensionChangeKind.User, merged.User, spec.Policy, evaluations,
            sp => UserEvaluation(sp, envelope.User));
        Evaluate(DimensionChangeKind.Regulatory, merged.Regulatory, spec.Policy, evaluations,
            sp => RegulatoryEvaluation(sp, envelope.Regulatory));
        Evaluate(DimensionChangeKind.Runtime, merged.Runtime, spec.Policy, evaluations,
            sp => RuntimeEvaluation(sp, envelope.Runtime));
        Evaluate(DimensionChangeKind.FormFactor, merged.FormFactor, spec.Policy, evaluations,
            sp => FormFactorEvaluation(sp, envelope.FormFactor));
        Evaluate(DimensionChangeKind.Edition, merged.Edition, spec.Policy, evaluations,
            sp => EditionEvaluation(sp, envelope.Edition));
        Evaluate(DimensionChangeKind.Network, merged.Network, spec.Policy, evaluations,
            sp => NetworkEvaluation(sp, envelope.Network));
        Evaluate(DimensionChangeKind.TrustAnchor, merged.Trust, spec.Policy, evaluations,
            sp => TrustEvaluation(sp, envelope.TrustAnchor));
        Evaluate(DimensionChangeKind.SyncState, merged.SyncState, spec.Policy, evaluations,
            sp => SyncStateEvaluation(sp, envelope.SyncState));
        Evaluate(DimensionChangeKind.VersionVector, merged.VersionVector, spec.Policy, evaluations,
            sp => VersionVectorEvaluation(sp, envelope.VersionVector));

        // Per A1.8 — verdict over Required first, then Recommended; Informational ignored for verdict.
        var hasRequiredFail = evaluations.Any(e => e.Policy == DimensionPolicyKind.Required && e.Outcome == DimensionPassFail.Fail);
        var hasRecommendedFail = evaluations.Any(e => e.Policy == DimensionPolicyKind.Recommended && e.Outcome == DimensionPassFail.Fail);
        var verdict =
            hasRequiredFail ? OverallVerdict.Block :
            hasRecommendedFail ? OverallVerdict.WarnOnly :
            OverallVerdict.Pass;

        var result = new SystemRequirementsResult
        {
            Overall = verdict,
            Dimensions = evaluations,
            EvaluatedAt = now,
        };

        _cache[key] = new CacheEntry(result, now);

        if (_auditTrail is not null && _signer is not null)
        {
            await EmitEvaluationAuditsAsync(spec, envelope, platform, result, now, ct).ConfigureAwait(false);
        }

        return result;
    }

    private async Task EmitEvaluationAuditsAsync(
        MinimumSpec spec,
        MissionEnvelope envelope,
        string? platform,
        SystemRequirementsResult result,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // MinimumSpecEvaluated — 5-minute dedup keyed on (spec, envelopeHash, platform).
        var evaluatedKey = $"evaluated:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(spec)}:{envelope.EnvelopeHash}:{platform ?? ""}";
        if (ShouldEmit(_evaluatedDedup, evaluatedKey, EvaluatedDedupWindow, now))
        {
            await EmitAsync(
                AuditEventType.MinimumSpecEvaluated,
                MissionSpaceAuditPayloads.MinimumSpecEvaluated(
                    envelope.EnvelopeHash,
                    result.Overall.ToString(),
                    result.Dimensions.Count,
                    platform),
                ct).ConfigureAwait(false);
        }

        // InstallBlocked / InstallWarned — per-attempt (no dedup; install attempts are rare).
        if (result.Overall == OverallVerdict.Block)
        {
            var failingDim = result.Dimensions
                .First(d => d.Policy == DimensionPolicyKind.Required && d.Outcome == DimensionPassFail.Fail)
                .Dimension;
            await EmitAsync(
                AuditEventType.InstallBlocked,
                MissionSpaceAuditPayloads.InstallBlocked(envelope.EnvelopeHash, failingDim.ToString(), platform),
                ct).ConfigureAwait(false);
        }
        else if (result.Overall == OverallVerdict.WarnOnly)
        {
            var warningDim = result.Dimensions
                .First(d => d.Policy == DimensionPolicyKind.Recommended && d.Outcome == DimensionPassFail.Fail)
                .Dimension;
            await EmitAsync(
                AuditEventType.InstallWarned,
                MissionSpaceAuditPayloads.InstallWarned(envelope.EnvelopeHash, warningDim.ToString(), platform),
                ct).ConfigureAwait(false);
        }

        // PostInstallSpecRegression — 24-hour dedup keyed on dimension; fires when a dimension
        // transitions Pass → Fail across consecutive evaluations of the same (spec, platform)
        // (envelope drift is the typical cause; the key intentionally omits envelopeHash so the
        // outcome history persists across snapshot changes).
        foreach (var dim in result.Dimensions)
        {
            var prevKey = (spec, platform, dim.Dimension);
            var hadPrevious = _previousOutcomes.TryGetValue(prevKey, out var prev);
            _previousOutcomes[prevKey] = dim.Outcome;
            if (hadPrevious && prev == DimensionPassFail.Pass && dim.Outcome == DimensionPassFail.Fail)
            {
                var regressionKey = $"regression:{dim.Dimension}:{platform ?? ""}";
                if (ShouldEmit(_regressionDedup, regressionKey, RegressionDedupWindow, now))
                {
                    await EmitAsync(
                        AuditEventType.PostInstallSpecRegression,
                        MissionSpaceAuditPayloads.PostInstallSpecRegression(
                            dim.Dimension.ToString(),
                            prev.ToString(),
                            dim.Outcome.ToString(),
                            envelope.EnvelopeHash),
                        ct).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool ShouldEmit(
        ConcurrentDictionary<string, DateTimeOffset> dedup,
        string key,
        TimeSpan window,
        DateTimeOffset now)
    {
        var fire = false;
        dedup.AddOrUpdate(
            key,
            _ => { fire = true; return now; },
            (_, last) =>
            {
                if (now - last >= window) { fire = true; return now; }
                return last;
            });
        return fire;
    }

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void InvalidateCache() => _cache.Clear();

    /// <summary>
    /// Per A1.7 COMPOSE — for each dimension, override replaces baseline if
    /// override declares it; otherwise baseline applies.
    /// </summary>
    internal static MergedSpec ComposeWithPlatform(MinimumSpec spec, string? platform)
    {
        if (string.IsNullOrEmpty(platform))
        {
            return new MergedSpec(
                spec.Hardware, spec.User, spec.Regulatory, spec.Runtime, spec.FormFactor,
                spec.Edition, spec.Network, spec.Trust, spec.SyncState, spec.VersionVector);
        }

        var override_ = spec.PerPlatform.FirstOrDefault(p => string.Equals(p.Platform, platform, StringComparison.OrdinalIgnoreCase));
        if (override_ is null)
        {
            return new MergedSpec(
                spec.Hardware, spec.User, spec.Regulatory, spec.Runtime, spec.FormFactor,
                spec.Edition, spec.Network, spec.Trust, spec.SyncState, spec.VersionVector);
        }

        return new MergedSpec(
            override_.Hardware ?? spec.Hardware,
            override_.User ?? spec.User,
            override_.Regulatory ?? spec.Regulatory,
            override_.Runtime ?? spec.Runtime,
            override_.FormFactor ?? spec.FormFactor,
            override_.Edition ?? spec.Edition,
            override_.Network ?? spec.Network,
            override_.Trust ?? spec.Trust,
            override_.SyncState ?? spec.SyncState,
            override_.VersionVector ?? spec.VersionVector);
    }

    private static void Evaluate<TSpec>(
        DimensionChangeKind dimension,
        TSpec? specSection,
        SpecPolicy policy,
        List<DimensionEvaluation> output,
        Func<TSpec, DimensionPassFail> evaluator)
        where TSpec : class
    {
        if (specSection is null)
        {
            // No spec for this dimension → unevaluated; surfaced for visibility.
            output.Add(new DimensionEvaluation
            {
                Dimension = dimension,
                Policy = DimensionPolicyKind.Unevaluated,
                Outcome = DimensionPassFail.Unevaluated,
            });
            return;
        }
        var dimPolicy = MapPolicy(policy);
        var outcome = evaluator(specSection);
        output.Add(new DimensionEvaluation
        {
            Dimension = dimension,
            Policy = dimPolicy,
            Outcome = outcome,
        });
    }

    private static DimensionPolicyKind MapPolicy(SpecPolicy p) => p switch
    {
        SpecPolicy.Required => DimensionPolicyKind.Required,
        SpecPolicy.Recommended => DimensionPolicyKind.Recommended,
        SpecPolicy.Informational => DimensionPolicyKind.Informational,
        _ => DimensionPolicyKind.Unevaluated,
    };

    // ===== Per-dimension evaluation rules =====

    private static DimensionPassFail HardwareEvaluation(HardwareSpec spec, HardwareCapabilities env)
    {
        if (spec.MinMemoryBytes is { } minMem
            && env.RamTotalMb is { } ramMb
            && (long)ramMb * 1024L * 1024L < minMem)
        {
            return DimensionPassFail.Fail;
        }
        if (spec.MinCpuLogicalCores is { } minCores
            && env.CpuLogicalCores is { } cores
            && cores < minCores)
        {
            return DimensionPassFail.Fail;
        }
        if (spec.RequiredCpuArchitectures is { Count: > 0 } archs
            && env.CpuArch is { } arch
            && !archs.Contains(arch))
        {
            return DimensionPassFail.Fail;
        }
        if (spec.RequiresGpu == true && env.HasGpu != true)
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail UserEvaluation(UserSpec spec, UserCapabilities env)
    {
        if (spec.RequiresSignIn == true && !env.IsSignedIn) return DimensionPassFail.Fail;
        if (spec.RequiredRoles is { Count: > 0 } roles)
        {
            foreach (var r in roles)
            {
                if (!env.Roles.Contains(r)) return DimensionPassFail.Fail;
            }
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail RegulatoryEvaluation(RegulatorySpec spec, RegulatoryCapabilities env)
    {
        if (spec.AllowedJurisdictions is { Count: > 0 } allowed)
        {
            var matches = env.JurisdictionCodes.Any(c => allowed.Contains(c));
            if (!matches) return DimensionPassFail.Fail;
        }
        if (spec.ProhibitedJurisdictions is { Count: > 0 } prohibited)
        {
            var prohibitedHit = env.JurisdictionCodes.Any(c => prohibited.Contains(c));
            if (prohibitedHit) return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail RuntimeEvaluation(RuntimeSpec spec, RuntimeCapabilities env)
    {
        if (spec.RequiredOsFamilies is { Count: > 0 } families
            && env.OsFamily is { } family
            && !families.Contains(family))
        {
            return DimensionPassFail.Fail;
        }
        if (spec.MinOsVersion is { } minOs
            && env.OsVersion is { } osVer
            && CompareSemver(osVer, minOs) < 0)
        {
            return DimensionPassFail.Fail;
        }
        if (spec.MinDotnetVersion is { } minDot
            && env.DotnetVersion is { } dotVer
            && CompareSemver(dotVer, minDot) < 0)
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail FormFactorEvaluation(FormFactorSpec spec, FormFactorSnapshot env)
    {
        if (spec.AcceptableFormFactors is { Count: > 0 } accepted
            && env.Profile is { } profile
            && !accepted.Contains(profile.FormFactor))
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail EditionEvaluation(EditionSpec spec, EditionCapabilities env)
    {
        if (spec.AllowedEditions is { Count: > 0 } allowed
            && env.EditionKey is { } key
            && !allowed.Contains(key))
        {
            return DimensionPassFail.Fail;
        }
        if (spec.TrialIsAcceptable == false && env.IsTrial == true)
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail NetworkEvaluation(NetworkSpec spec, NetworkCapabilities env)
    {
        if (spec.RequiresOnline == true && !env.IsOnline) return DimensionPassFail.Fail;
        if (spec.RejectsMeteredConnection == true && env.IsMeteredConnection == true) return DimensionPassFail.Fail;
        // Phase 1 substrate: RequiredTransports surfacing requires per-host
        // wiring (envelope's NetworkCapabilities does not enumerate active
        // transports). Treat as Pass when no transport probe is wired.
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail TrustEvaluation(TrustSpec spec, TrustAnchorCapabilities env)
    {
        if (spec.RequiresIdentityKey == true && !env.HasIdentityKey) return DimensionPassFail.Fail;
        if (spec.MinTrustedPeerCount is { } min
            && env.TrustedPeerCount is { } count
            && count < min)
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail SyncStateEvaluation(SyncStateSpec spec, SyncStateSnapshot env)
    {
        if (spec.AcceptableStates is { Count: > 0 } accepted
            && !accepted.Contains(env.State))
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    private static DimensionPassFail VersionVectorEvaluation(VersionVectorSpec spec, VersionVectorSnapshot env)
    {
        if (spec.MinKernelVersion is { } minKern
            && env.Vector is { } vector
            && CompareSemver(vector.Kernel, minKern) < 0)
        {
            return DimensionPassFail.Fail;
        }
        if (spec.MinSchemaEpoch is { } minEp
            && env.Vector is { } vec
            && vec.SchemaEpoch < minEp)
        {
            return DimensionPassFail.Fail;
        }
        return DimensionPassFail.Pass;
    }

    /// <summary>Lexical SemVer compare on the leading numeric segments. Returns &lt; 0 / 0 / &gt; 0.</summary>
    private static int CompareSemver(string a, string b)
    {
        var aParts = a.Split('.', '-');
        var bParts = b.Split('.', '-');
        var len = Math.Min(aParts.Length, bParts.Length);
        for (int i = 0; i < len; i++)
        {
            if (int.TryParse(aParts[i], out var ax) && int.TryParse(bParts[i], out var bx))
            {
                var cmp = ax.CompareTo(bx);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = string.CompareOrdinal(aParts[i], bParts[i]);
                if (cmp != 0) return cmp;
            }
        }
        return aParts.Length.CompareTo(bParts.Length);
    }

    internal sealed record MergedSpec(
        HardwareSpec? Hardware,
        UserSpec? User,
        RegulatorySpec? Regulatory,
        RuntimeSpec? Runtime,
        FormFactorSpec? FormFactor,
        EditionSpec? Edition,
        NetworkSpec? Network,
        TrustSpec? Trust,
        SyncStateSpec? SyncState,
        VersionVectorSpec? VersionVector);

    private readonly record struct CacheKey(MinimumSpec Spec, string EnvelopeHash, string? Platform);

    private readonly record struct CacheEntry(SystemRequirementsResult Result, DateTimeOffset At);
}
