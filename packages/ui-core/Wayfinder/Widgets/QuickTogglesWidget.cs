using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder.Widgets;

/// <summary>
/// Quick-toggles Helm widget per ADR 0066 §1.4 (W#53 Phase 2 PR
/// 2b). Surfaces three single-tap Standing-Order toggles on the
/// <see cref="HelmSlot.ActionStack"/>: offline mode (Platform-scope),
/// do-not-disturb (User-scope), pause-sync (Platform-scope).
/// </summary>
/// <remarks>
/// <para>
/// <b>Slot:</b> <see cref="HelmSlot.ActionStack"/>; OrderHint 100
/// (first widget in the ActionStack column).
/// </para>
/// <para>
/// <b>Read-only ComputeAsync:</b> per ADR 0066 §1.1 contract +
/// W#53 hand-off line 663-666, this widget MUST NOT call
/// <c>IStandingOrderIssuer.IssueAsync</c> from
/// <see cref="ComputeAsync"/>. Toggles are surfaced as
/// <see cref="HelmWidgetAction"/> entries with
/// <see cref="HelmActionInvocationKind.IssueStandingOrder"/>; the
/// adapter renderer (Phase 2c) is responsible for invoking the
/// issuer when the operator activates the toggle.
/// </para>
/// <para>
/// <b>Reactive refresh (W#57 cleared):</b> per the W#57 hand-off
/// (PR #662 merged), <c>StandingOrderAppliedEvent</c> +
/// <c>IStandingOrderEventStream</c> are now on origin/main. ui-core
/// cannot subscribe directly (cycle:
/// <c>ui-core → foundation-wayfinder → kernel-crdt → ui-core</c>),
/// but Phase 3 may wire a cycle-safe
/// <c>IRecentStandingOrdersSource</c>-style refresh hook. For
/// Phase 2b, the widget renders deterministic toggle actions; the
/// adapter's periodic refresh tick (per
/// <see cref="HelmOptions.PeriodicRefreshInterval"/>) keeps the
/// view-state fresh without an in-package event subscription.
/// </para>
/// <para>
/// <b>Scope discipline:</b> Path / Scope tuples encode each
/// toggle's intent:
/// <list type="bullet">
/// <item><description><c>system.network.offline</c> →
/// <c>StandingOrderScope.Platform</c> (network-mode spans tenants
/// on the local node).</description></item>
/// <item><description><c>system.notifications.dnd</c> →
/// <c>StandingOrderScope.User</c> (per-user notification preference).</description></item>
/// <item><description><c>system.sync.paused</c> →
/// <c>StandingOrderScope.Platform</c> (sync state spans
/// tenants).</description></item>
/// </list>
/// <c>StandingOrderScope.System</c> does not exist — the live enum
/// values are <c>User / Tenant / Platform / Integration / Security</c>.
/// The Target string format is
/// <c>"{Path}|{Scope}"</c> so the renderer can deterministically
/// extract both fields when constructing the Standing-Order draft.
/// </para>
/// </remarks>
public sealed class QuickTogglesWidget : IHelmWidget
{
    private static readonly HelmWidgetAction[] StaticActions =
    {
        new(
            ActionId: "offline-mode",
            AccessibleLabel: "Toggle offline mode",
            Kind: HelmActionInvocationKind.IssueStandingOrder,
            Target: "system.network.offline|Platform"),
        new(
            ActionId: "dnd-mode",
            AccessibleLabel: "Toggle do-not-disturb",
            Kind: HelmActionInvocationKind.IssueStandingOrder,
            Target: "system.notifications.dnd|User"),
        new(
            ActionId: "pause-sync",
            AccessibleLabel: "Toggle pause sync",
            Kind: HelmActionInvocationKind.IssueStandingOrder,
            Target: "system.sync.paused|Platform"),
    };

    /// <inheritdoc />
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "quick-toggles",
        Slot: HelmSlot.ActionStack,
        OrderHint: 100,
        AccessibleName: "Quick toggles",
        CapabilityGateType: null);

    /// <inheritdoc />
    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var view = new HelmWidgetViewState(
            State: SyncState.Healthy,
            PrimaryLabel: "Quick toggles",
            SecondaryLabel: null,
            Actions: StaticActions);
        return ValueTask.FromResult(view);
    }
}
