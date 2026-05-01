using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Reference <see cref="ICompatibilityRelation"/> implementing the 6 rules
/// of ADR 0028-A6.2 with the A7.3 augmentation. Evaluation order is
/// schema-epoch first (hard rejection per A6.2 rule 1), then the other
/// rules in numeric order; the first rule that rejects wins.
/// </summary>
/// <remarks>
/// <para>
/// All rules are symmetric in their inputs: <c>Evaluate(v1, v2)</c> and
/// <c>Evaluate(v2, v1)</c> return the same verdict. This satisfies A7.1's
/// two-phase commit convergence requirement — both peers compute the same
/// verdict from their local view + the wire-format vector they receive.
/// </para>
/// </remarks>
public sealed class DefaultCompatibilityRelation : ICompatibilityRelation
{
    /// <summary>Default kernel SemVer minor-version lag tolerated by rule 2 (A6.2).</summary>
    public const uint DefaultMaxKernelMinorLag = 2;

    private readonly uint _maxKernelMinorLag;

    /// <summary>Constructs the default relation with the canonical 2-minor-version lag tolerance.</summary>
    public DefaultCompatibilityRelation() : this(DefaultMaxKernelMinorLag) { }

    /// <summary>Constructs the relation with a tunable kernel-minor lag (A7.2 makes this knob explicit).</summary>
    public DefaultCompatibilityRelation(uint maxKernelMinorLag)
    {
        _maxKernelMinorLag = maxKernelMinorLag;
    }

    public VersionVectorVerdict Evaluate(VersionVector v1, VersionVector v2)
    {
        ArgumentNullException.ThrowIfNull(v1);
        ArgumentNullException.ThrowIfNull(v2);

        // Rule 1 — schema-epoch must match exactly (hard rejection per A6.2).
        if (v1.SchemaEpoch != v2.SchemaEpoch)
        {
            return Reject(
                FailedRule.SchemaEpochMismatch,
                $"schema epochs differ: {v1.SchemaEpoch} vs {v2.SchemaEpoch}");
        }

        // Rule 2 — kernel SemVer minor-version window.
        if (!TryParseSemver(v1.Kernel, out var k1Major, out var k1Minor))
        {
            return Reject(FailedRule.KernelSemverWindow, $"kernel '{v1.Kernel}' is not a valid SemVer");
        }
        if (!TryParseSemver(v2.Kernel, out var k2Major, out var k2Minor))
        {
            return Reject(FailedRule.KernelSemverWindow, $"kernel '{v2.Kernel}' is not a valid SemVer");
        }
        if (k1Major != k2Major)
        {
            return Reject(
                FailedRule.KernelSemverWindow,
                $"kernel majors differ: {v1.Kernel} vs {v2.Kernel}");
        }
        var lag = k1Minor > k2Minor ? k1Minor - k2Minor : k2Minor - k1Minor;
        if (lag > _maxKernelMinorLag)
        {
            return Reject(
                FailedRule.KernelSemverWindow,
                $"kernel minor lag {lag} exceeds window {_maxKernelMinorLag}: {v1.Kernel} vs {v2.Kernel}");
        }

        // Rule 3 — required-plugin set intersection (A7.3 augmentation:
        // each side reads the Required flag from the wire-format entry,
        // and required-on-either-side means required-on-both-sides).
        var allPluginIds = new HashSet<PluginId>(v1.Plugins.Keys);
        allPluginIds.UnionWith(v2.Plugins.Keys);
        foreach (var pluginId in allPluginIds)
        {
            var v1Has = v1.Plugins.TryGetValue(pluginId, out var v1Entry);
            var v2Has = v2.Plugins.TryGetValue(pluginId, out var v2Entry);
            var requiredOnEither = (v1Has && v1Entry!.Required) || (v2Has && v2Entry!.Required);
            if (requiredOnEither && !(v1Has && v2Has))
            {
                return Reject(
                    FailedRule.RequiredPluginIntersection,
                    $"plugin '{pluginId}' is required on one side but absent on the other");
            }
        }

        // Rule 4 — adapter-set intersection (asymmetry alone is fine; only
        // an empty intersection blocks federation).
        if (v1.Adapters.Count > 0 && v2.Adapters.Count > 0)
        {
            var intersection = v1.Adapters.Keys.Intersect(v2.Adapters.Keys).Any();
            if (!intersection)
            {
                return Reject(
                    FailedRule.AdapterSetIncompatible,
                    "adapter sets share no common keys");
            }
        }

        // Rule 5 — release-channel ordering (A7.9 reword: pairing across
        // channels is rejected; same-channel pairing is required).
        if (v1.Channel != v2.Channel)
        {
            return Reject(
                FailedRule.ChannelOrdering,
                $"channels differ: {v1.Channel} vs {v2.Channel}");
        }

        // Rule 6 — instance-class compatibility. Per A6.2 rule 6 / A7.6,
        // cross-instance pairing is OK by default; v0 names no specific
        // blocking pairs, so this rule always passes here. Future
        // amendments will introduce blocking pairs as forcing functions
        // surface (e.g., a SelfHost-only feature lands and must reject
        // ManagedBridge peers).
        _ = v1.InstanceClass;
        _ = v2.InstanceClass;

        return new VersionVectorVerdict(VerdictKind.Compatible, FailedRule: null, FailedRuleDetail: null);
    }

    private static VersionVectorVerdict Reject(FailedRule rule, string detail)
        => new(VerdictKind.Incompatible, rule, detail);

    private static bool TryParseSemver(string s, out uint major, out uint minor)
    {
        major = 0;
        minor = 0;
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        var parts = s.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }
        if (!uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out major))
        {
            return false;
        }
        if (!uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minor))
        {
            return false;
        }
        return true;
    }
}
