using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference <see cref="IFirstAidSurface"/> per ADR 0082 §4 +
/// W#54 Phase 2. Ships a hardcoded hint library covering surface keys
/// <c>"pharmacy"</c>, <c>"lab"</c>, <c>"atmosphere"</c>. Sibling-block
/// surface keys (<c>"engine-room"</c>, <c>"quarterdeck"</c>,
/// <c>"tactical"</c>) are NOT covered here — those teams register their
/// own hints via DI in their own block packages.
/// </summary>
/// <remarks>
/// <para>
/// All hints pass <see cref="FirstAidHint"/>'s plain-text validation
/// (no HTML metacharacters; no non-LF control chars). Renderers can
/// emit <see cref="FirstAidHint.Body"/> verbatim with no per-renderer
/// escaping. Per ADR 0082 §Trust impact.
/// </para>
/// <para>
/// Unknown surface keys return an empty list per the graceful-
/// degradation contract on <see cref="IFirstAidSurface.GetContextualHintsAsync"/>.
/// Lookup is case-sensitive on the canonical kebab-case key.
/// </para>
/// </remarks>
internal sealed class DefaultFirstAidSurface : IFirstAidSurface
{
    private static readonly IReadOnlyList<FirstAidHint> NoHints = [];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<FirstAidHint>> HintLibrary =
        new Dictionary<string, IReadOnlyList<FirstAidHint>>
        {
            ["pharmacy"] =
            [
                new FirstAidHint(
                    Key: "sick-bay.pharmacy.k-anonymity",
                    Title: "Counts below 3 are suppressed",
                    Body: "Pharmacy record counts under 3 appear as Suppressed. This protects "
                        + "individual recovery records from inference attacks.",
                    Level: FirstAidLevel.Info),
                new FirstAidHint(
                    Key: "sick-bay.pharmacy.rotation-overdue",
                    Title: "Rotation overdue",
                    Body: "Fields tagged Rotation Overdue have exceeded the configured rotation "
                        + "window. Trigger a rotation from this row to bring the field back into "
                        + "the Current band.",
                    Level: FirstAidLevel.Warning),
            ],
            ["lab"] =
            [
                new FirstAidHint(
                    Key: "sick-bay.lab.probe-degraded",
                    Title: "Probe in degraded state",
                    Body: "A probe reporting Yellow or Orange degradation indicates the underlying "
                        + "capability is functioning but reduced. Investigate before relying on the "
                        + "Mission Envelope dimension this probe feeds.",
                    Level: FirstAidLevel.Warning),
                new FirstAidHint(
                    Key: "sick-bay.lab.probe-critical",
                    Title: "Critical probe failure",
                    Body: "A Red probe indicates the capability is unavailable. The Mission Envelope "
                        + "may be marking dependent features as Disabled until the probe recovers.",
                    Level: FirstAidLevel.Warning),
            ],
            ["atmosphere"] =
            [
                new FirstAidHint(
                    Key: "sick-bay.atmosphere.thresholds",
                    Title: "Health bands",
                    Body: "Green: all probes healthy. Yellow: at least one warning, no criticals. "
                        + "Orange: any critical OR multiple warnings. Red: 3+ criticals OR an active "
                        + "force-enable override.",
                    Level: FirstAidLevel.Info),
                new FirstAidHint(
                    Key: "sick-bay.atmosphere.force-enable",
                    Title: "Force-enable override active",
                    Body: "When the Atmosphere readout shows Red with a force-enable banner, an "
                        + "operator has bypassed a probe gate. Review the override record before "
                        + "trusting downstream Mission Envelope decisions.",
                    Level: FirstAidLevel.Warning),
            ],
        };

    /// <inheritdoc />
    public Task<IReadOnlyList<FirstAidHint>> GetContextualHintsAsync(
        string surfaceKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(surfaceKey))
        {
            return Task.FromResult(NoHints);
        }

        return Task.FromResult(
            HintLibrary.TryGetValue(surfaceKey, out var hints) ? hints : NoHints);
    }
}
