using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder.Widgets;

/// <summary>
/// Recent Standing Orders Helm widget per ADR 0066 §1.4 (W#53
/// Phase 2 PR 2b). Surfaces the 5 most recently-issued Standing
/// Orders for the ambient actor on the
/// <see cref="HelmSlot.ActivityFeed"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slot:</b> <see cref="HelmSlot.ActivityFeed"/>; OrderHint 100
/// (first widget in the ActivityFeed column).
/// </para>
/// <para>
/// <b>Cycle-safe data source:</b> the widget consumes
/// <see cref="IRecentStandingOrdersSource"/> rather than
/// <c>foundation-wayfinder</c>'s
/// <c>IStandingOrderRepository</c> directly — ui-core cannot
/// reference foundation-wayfinder (cycle:
/// <c>ui-core → foundation-wayfinder → kernel-crdt → ui-core</c>).
/// Phase 3 wires a cycle-safe implementation that projects
/// <c>StandingOrder</c> records into
/// <see cref="RecentStandingOrderEntry"/> DTOs at the boundary.
/// </para>
/// <para>
/// <b>Optional dependency:</b> the source is constructor-injected
/// as nullable. Hosts that have not yet wired the foundation-tier
/// implementation (Phase 2 dev / kitchen-sink hosts before Phase 3
/// lands) get a graceful "No recent orders" rendering rather than a
/// DI resolution failure.
/// </para>
/// <para>
/// <b>Reactive refresh (W#57 cleared):</b> Phase 3
/// implementations of <see cref="IRecentStandingOrdersSource"/>
/// MAY subscribe to <c>IStandingOrderEventStream</c> internally to
/// invalidate their per-actor cache on
/// <c>StandingOrderAppliedEvent</c>. The widget itself does NOT
/// subscribe — periodic refresh per
/// <see cref="HelmOptions.PeriodicRefreshInterval"/> drives the
/// view tick.
/// </para>
/// </remarks>
public sealed class RecentStandingOrdersWidget : IHelmWidget
{
    /// <summary>Maximum entries the widget surfaces per ADR 0066 §1.4.</summary>
    public const int MaxEntries = 5;

    private readonly IRecentStandingOrdersSource? _source;

    /// <summary>
    /// Construct the widget. <paramref name="source"/> is optional —
    /// when null the widget renders "No recent orders" without
    /// raising a DI resolution error.
    /// </summary>
    public RecentStandingOrdersWidget(IRecentStandingOrdersSource? source = null)
    {
        _source = source;
    }

    /// <inheritdoc />
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "recent-standing-orders",
        Slot: HelmSlot.ActivityFeed,
        OrderHint: 100,
        AccessibleName: "Recent standing orders",
        CapabilityGateType: null);

    /// <inheritdoc />
    public async ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_source is null)
        {
            return new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: "Recent standing orders",
                SecondaryLabel: "No recent orders",
                Actions: Array.Empty<HelmWidgetAction>());
        }

        IReadOnlyList<RecentStandingOrderEntry> entries;
        try
        {
            entries = await _source.GetRecentAsync(
                context.Tenant,
                context.Actor,
                MaxEntries,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException
                or IOException
                or TimeoutException
                or HttpRequestException)
        {
            // Transient infrastructure faults degrade to a stale-state
            // surface rather than propagating into the Helm render —
            // a repository hiccup must not blank the entire Helm.
            // Programmer-error exceptions (NullReferenceException,
            // ArgumentException, ...) deliberately propagate so they
            // surface during dev / test rather than getting silently
            // swallowed (council m-3 amendment).
            return new HelmWidgetViewState(
                State: SyncState.Stale,
                PrimaryLabel: "Recent standing orders",
                SecondaryLabel: "Source unavailable",
                Actions: Array.Empty<HelmWidgetAction>());
        }

        // Per IRecentStandingOrdersSource contract, implementations
        // MUST NOT return null — the empty-list path covers the "no
        // history" case. The defensive null guard below is
        // belt-and-suspenders against a contract violation; the
        // empty-list path is the documented happy case.
        if (entries is null || entries.Count == 0)
        {
            return new HelmWidgetViewState(
                State: SyncState.Healthy,
                PrimaryLabel: "Recent standing orders",
                SecondaryLabel: "No recent orders",
                Actions: Array.Empty<HelmWidgetAction>());
        }

        var actions = new HelmWidgetAction[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            actions[i] = new HelmWidgetAction(
                ActionId: $"recent-order-{i + 1}",
                AccessibleLabel: $"View {entry.Path} (issued by {entry.IssuedByDisplayName})",
                Kind: HelmActionInvocationKind.Navigate,
                Target: $"wayfinder/standing-orders/{entry.StandingOrderId.Value:N}");
        }

        return new HelmWidgetViewState(
            State: SyncState.Healthy,
            PrimaryLabel: "Recent standing orders",
            SecondaryLabel: $"{entries.Count} recent",
            Actions: actions);
    }
}
