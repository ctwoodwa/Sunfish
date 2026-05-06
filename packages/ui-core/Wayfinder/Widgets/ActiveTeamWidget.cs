using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder.Widgets;

/// <summary>
/// Active-team Helm widget per ADR 0066 §1.4 (W#53 Phase 2 PR 2a).
/// Renders the actor's currently-active team identifier — surfaced
/// from <see cref="HelmRenderContext.ActiveTeamId"/>. Anchor surfaces
/// pass a non-null value when a team is selected; Bridge tenant
/// surfaces pass null (single-team-per-tenant assumption) and the
/// widget renders a "no active team" affordance.
/// </summary>
/// <remarks>
/// <b>Slot:</b> <see cref="HelmSlot.GlanceBand"/>; OrderHint 300.
/// The widget intentionally does NOT resolve the team's display
/// name — that requires a kernel-runtime read seam which would
/// pull a runtime ProjectReference into ui-core (cycle: see
/// <see cref="HelmRenderContext.ActiveTeamId"/> rationale). Anchor
/// adapter renderers may resolve the display name at the boundary
/// and override the widget's label via the ambient i18n surface; the
/// widget contract surfaces the canonical id form only.
/// </remarks>
public sealed class ActiveTeamWidget : IHelmWidget
{
    /// <inheritdoc />
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "active-team",
        Slot: HelmSlot.GlanceBand,
        OrderHint: 300,
        AccessibleName: "Active team",
        CapabilityGateType: null);

    /// <inheritdoc />
    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var teamId = context.ActiveTeamId;
        var view = teamId is null
            ? new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: "No active team",
                SecondaryLabel: null,
                Actions: new HelmWidgetAction[]
                {
                    new(
                        ActionId: "select-team",
                        AccessibleLabel: "Select team",
                        Kind: HelmActionInvocationKind.Navigate,
                        Target: "wayfinder/teams"),
                })
            : new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: $"Team {teamId.Value:N}",
                SecondaryLabel: null,
                Actions: new HelmWidgetAction[]
                {
                    new(
                        ActionId: "switch-team",
                        AccessibleLabel: "Switch team",
                        Kind: HelmActionInvocationKind.Navigate,
                        Target: "wayfinder/teams"),
                });
        return ValueTask.FromResult(view);
    }
}
