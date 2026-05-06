using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder.Widgets;

/// <summary>
/// Mission-envelope summary Helm widget per ADR 0066 §1.4 (W#53
/// Phase 2 PR 2a). Renders a coarse-grained summary of the ambient
/// <see cref="Sunfish.Foundation.MissionSpace.MissionEnvelope"/>'s
/// capability dimensions — the operator sees one-line status
/// without drilling into individual feature gates.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slot:</b> <see cref="HelmSlot.GlanceBand"/>; OrderHint 400.
/// </para>
/// <para>
/// <b>Phase-2 surface:</b> the widget summarizes envelope coverage
/// by counting the dimensions that have a present capability
/// snapshot (Hardware / User / Regulatory / Runtime / FormFactor /
/// Edition / Network / TrustAnchor / SyncState / VersionVector —
/// ten required dimensions per ADR 0062). When all are present the
/// widget renders <see cref="SyncState.Healthy"/>; otherwise
/// <see cref="SyncState.Stale"/> with an indication of what's
/// missing. A future amendment will expand to per-feature-gate
/// verdicts when an <c>IFeatureVerdictProvider</c> seam ships.
/// </para>
/// </remarks>
public sealed class MissionEnvelopeSummaryWidget : IHelmWidget
{
    /// <inheritdoc />
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "mission-envelope-summary",
        Slot: HelmSlot.GlanceBand,
        OrderHint: 400,
        AccessibleName: "Mission envelope",
        CapabilityGateType: null);

    /// <inheritdoc />
    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var envelope = context.Envelope;

        // ADR 0062-A1.2 mandates ten dimensions and MissionEnvelope.cs
        // declares all of them 'required' — so the count is structural,
        // not runtime. We carry the enumeration anyway so a future
        // schema change (optional dimensions, deferred dimensions)
        // can pivot this widget to a missing-list rendering without a
        // contract break.
        var dimensions = new (string Name, bool Present)[]
        {
            ("Hardware", envelope.Hardware is not null),
            ("User", envelope.User is not null),
            ("Regulatory", envelope.Regulatory is not null),
            ("Runtime", envelope.Runtime is not null),
            ("FormFactor", envelope.FormFactor is not null),
            ("Edition", envelope.Edition is not null),
            ("Network", envelope.Network is not null),
            ("Trust", envelope.TrustAnchor is not null),
            ("Sync", envelope.SyncState is not null),
            ("VersionVector", envelope.VersionVector is not null),
        };

        var presentCount = 0;
        for (var i = 0; i < dimensions.Length; i++)
        {
            if (dimensions[i].Present) presentCount++;
        }

        var allPresent = presentCount == dimensions.Length;
        var view = allPresent
            ? new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: "Mission envelope",
                SecondaryLabel: $"{presentCount} dimensions active",
                Actions: Array.Empty<HelmWidgetAction>())
            : new HelmWidgetViewState(
                State: SyncState.Stale,
                PrimaryLabel: "Mission envelope",
                SecondaryLabel: $"{presentCount}/{dimensions.Length} dimensions active",
                Actions: new HelmWidgetAction[]
                {
                    new(
                        ActionId: "view-envelope",
                        AccessibleLabel: "View mission envelope",
                        Kind: HelmActionInvocationKind.Navigate,
                        Target: "wayfinder/mission-envelope"),
                });
        return ValueTask.FromResult(view);
    }
}
