using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Reference <see cref="ICompositeJurisdictionProbe"/> per ADR 0064-A1.5 +
/// A1.15. Pure-function composition of 3 signals + tie-breaker resolution.
/// </summary>
public sealed class DefaultCompositeJurisdictionProbe : ICompositeJurisdictionProbe
{
    private readonly TimeProvider _time;
    private readonly RegulatoryAuditEmitter? _emitter;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultCompositeJurisdictionProbe(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultCompositeJurisdictionProbe(RegulatoryAuditEmitter emitter, TimeProvider? time = null)
        : this(time)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        _emitter = emitter;
    }

    /// <inheritdoc />
    public async ValueTask<JurisdictionProbe?> ProbeAsync(CompositeJurisdictionSignals signals, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ct.ThrowIfCancellationRequested();

        var present = 0;
        if (!string.IsNullOrEmpty(signals.UserDeclaredCode)) present++;
        if (!string.IsNullOrEmpty(signals.TenantConfigCode)) present++;
        if (!string.IsNullOrEmpty(signals.IpGeoCode)) present++;

        if (present == 0)
        {
            return null;
        }

        // Resolve the jurisdiction code via the A1.15 tie-breaker priority:
        // user-declaration > tenant-config > IP-geo.
        var resolved =
            !string.IsNullOrEmpty(signals.UserDeclaredCode) ? signals.UserDeclaredCode! :
            !string.IsNullOrEmpty(signals.TenantConfigCode) ? signals.TenantConfigCode! :
            signals.IpGeoCode!;

        // Count signals that AGREE with the resolved code.
        var agree = 0;
        var sources = new List<string>(3);
        if (string.Equals(signals.UserDeclaredCode, resolved, StringComparison.Ordinal))
        {
            agree++;
            sources.Add("user-declaration");
        }
        if (string.Equals(signals.TenantConfigCode, resolved, StringComparison.Ordinal))
        {
            agree++;
            sources.Add("tenant-config");
        }
        if (string.Equals(signals.IpGeoCode, resolved, StringComparison.Ordinal))
        {
            agree++;
            sources.Add("ip-geo");
        }

        // Confidence ladder: if every present signal agrees AND there are 3
        // present, High; if every present signal agrees AND there are 2,
        // Medium; if only 1 signal is present, Low; if any disagreement,
        // Low (tie-breaker resolved but confidence drops).
        var confidence =
            (agree == present && present == 3) ? Confidence.High :
            (agree == present && present == 2) ? Confidence.Medium :
            Confidence.Low;

        var probe = new JurisdictionProbe
        {
            JurisdictionCode = resolved,
            Confidence = confidence,
            SignalSources = sources,
            ProbedAt = _time.GetUtcNow(),
        };

        if (_emitter is not null && confidence == Confidence.Low)
        {
            // JurisdictionProbedWithLowConfidence — 1-hour dedup keyed on jurisdiction per A1.7.
            await _emitter.EmitWithRuleDedupAsync(
                $"low-confidence-probe:{resolved}",
                AuditEventType.JurisdictionProbedWithLowConfidence,
                RegulatoryAuditPayloads.JurisdictionProbedWithLowConfidence(resolved, sources.Count),
                ct).ConfigureAwait(false);
        }

        return probe;
    }
}
