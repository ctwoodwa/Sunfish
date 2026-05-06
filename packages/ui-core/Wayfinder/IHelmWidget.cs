using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Pluggable Helm-shell widget per ADR 0066 §1.1. Widgets render into
/// one of three Helm slots (<see cref="HelmSlot.GlanceBand"/> /
/// <see cref="HelmSlot.ActionStack"/> / <see cref="HelmSlot.ActivityFeed"/>);
/// the shell composes the registered set, ordered by
/// <see cref="HelmWidgetMetadata.OrderHint"/>, into the visible Helm.
/// </summary>
public interface IHelmWidget
{
    /// <summary>Static metadata for slot routing + capability-gate composition.</summary>
    HelmWidgetMetadata Metadata { get; }

    /// <summary>
    /// Compute the widget's current view-state given the ambient render
    /// context. Implementations MUST be side-effect-free and idempotent
    /// (the periodic refresh tick fires on
    /// <see cref="HelmOptions.PeriodicRefreshInterval"/>).
    /// </summary>
    ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Static metadata about a Helm widget per ADR 0066 §1.1.
/// </summary>
/// <param name="WidgetId">Stable identifier (kebab-case; e.g., <c>"recovery-readiness"</c>).</param>
/// <param name="Slot">Which Helm slot this widget renders into.</param>
/// <param name="OrderHint">Sort key within the slot; lower = earlier. Ties resolve by widget-id.</param>
/// <param name="AccessibleName">Localized accessible name surfaced via <c>aria-label</c> on the widget container.</param>
/// <param name="CapabilityGateType">
/// Optional CLR <see cref="Type"/> reference for an
/// <c>ICapabilityGate&lt;T&gt;</c> implementation. Reflection-bound at
/// composition time per ADR 0066 §1.2 — null means the widget has no
/// capability gate. <see cref="Type"/> is used here (rather than a
/// generic constraint) because <c>ICapabilityGate&lt;T&gt;</c> is not
/// yet on origin/main; the <see cref="Type"/> reference compiles
/// without it.
/// </param>
public sealed record HelmWidgetMetadata(
    string WidgetId,
    HelmSlot Slot,
    int OrderHint,
    string AccessibleName,
    Type? CapabilityGateType);

/// <summary>
/// Helm slot discriminator per ADR 0066 §1.1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HelmSlot
{
    /// <summary>Top-of-Helm summary band; status-glance widgets.</summary>
    GlanceBand,

    /// <summary>Mid-Helm action stack; primary-action widgets.</summary>
    ActionStack,

    /// <summary>Bottom-of-Helm activity feed; recent-event widgets.</summary>
    ActivityFeed,
}

/// <summary>
/// Materialized view-state for a Helm widget per ADR 0066 §1.1.
/// </summary>
/// <param name="State">Sync-state discriminator per ADR 0036 / foundation-ui-syncstate.</param>
/// <param name="PrimaryLabel">Localized primary label (rendered as widget headline).</param>
/// <param name="SecondaryLabel">Localized secondary label (rendered as widget caption); null when no secondary text.</param>
/// <param name="Actions">Per-widget actions surfaced as buttons / links.</param>
public sealed record HelmWidgetViewState(
    SyncState State,
    string PrimaryLabel,
    string? SecondaryLabel,
    IReadOnlyList<HelmWidgetAction> Actions);

/// <summary>
/// One affordance on a Helm widget per ADR 0066 §1.1.
/// </summary>
/// <param name="ActionId">Stable identifier (kebab-case).</param>
/// <param name="AccessibleLabel">Localized accessible label.</param>
/// <param name="Kind">Discriminator for what invoking the action does.</param>
/// <param name="Target">
/// Kind-specific target: a route path for <see cref="HelmActionInvocationKind.Navigate"/>;
/// a Standing Order draft URI for <see cref="HelmActionInvocationKind.IssueStandingOrder"/>;
/// a command identifier for <see cref="HelmActionInvocationKind.RunLocalCommand"/>.
/// <para>
/// <b><see cref="HelmActionInvocationKind.IssueStandingOrder"/>
/// draft URI format (per W#53 P2 PR 2b precedent):</b>
/// <c>"{Path}|{Scope}"</c> where <c>Path</c> is the Wayfinder
/// path the order targets and <c>Scope</c> is the canonical
/// <c>Sunfish.Foundation.Wayfinder.StandingOrderScope</c> enum
/// member name (live values: <c>User</c> / <c>Tenant</c> /
/// <c>Platform</c> / <c>Integration</c> / <c>Security</c>).
/// Renderers parse on the first <c>'|'</c> and resolve the scope
/// via <c>Enum.Parse&lt;StandingOrderScope&gt;(token, ignoreCase: false)</c>.
/// Example: <c>"system.network.offline|Platform"</c>. A future
/// amendment may introduce a strongly-typed action overload that
/// carries <c>(Path, Scope)</c> as a value-object, removing the
/// string-parse step.
/// </para>
/// </param>
public sealed record HelmWidgetAction(
    string ActionId,
    string AccessibleLabel,
    HelmActionInvocationKind Kind,
    string Target);

/// <summary>
/// Discriminator for <see cref="HelmWidgetAction.Kind"/> per ADR 0066 §1.1.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HelmActionInvocationKind
{
    /// <summary>Navigate to a route. <see cref="HelmWidgetAction.Target"/> is a route path.</summary>
    Navigate,

    /// <summary>Issue a Standing Order. <see cref="HelmWidgetAction.Target"/> is a Standing Order draft URI.</summary>
    IssueStandingOrder,

    /// <summary>Run a local command. <see cref="HelmWidgetAction.Target"/> is a command identifier (kebab-case).</summary>
    RunLocalCommand,
}

/// <summary>
/// Ambient context passed to <see cref="IHelmWidget.ComputeAsync"/> per
/// ADR 0066 §1.1.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset"/> stands in for the hand-off's
/// <c>NodaTime.Instant</c> per W#46 / W#49 / W#50 / W#54 / W#55 cohort
/// precedent — NodaTime is not on <c>Directory.Packages.props</c>;
/// future ADR amendment will migrate every cohort time-bearing record
/// at once.
/// </remarks>
/// <param name="Envelope">Current Mission Envelope.</param>
/// <param name="Tenant">Ambient tenant.</param>
/// <param name="Actor">Ambient actor.</param>
/// <param name="ActiveTeamId">
/// Identifier of the currently-active team in the multi-team Anchor
/// surface, or <c>null</c> when no team is selected (Bridge tenant case).
/// Typed as <see cref="Guid"/> rather than
/// <c>Sunfish.Kernel.Runtime.Teams.TeamId</c> because <c>kernel-runtime</c>
/// already references <c>ui-core</c> — bringing <c>TeamId</c> into the
/// shared <c>HelmRenderContext</c> would form a circular dependency.
/// Consumers wrap the Guid back into <c>TeamId</c> at the kernel-runtime
/// boundary.
/// </param>
/// <param name="Now">Wall-clock time the render context was materialized.</param>
public sealed record HelmRenderContext(
    MissionEnvelope Envelope,
    TenantId Tenant,
    ActorId Actor,
    Guid? ActiveTeamId,
    DateTimeOffset Now);

/// <summary>
/// Host-configurable Helm tunables per ADR 0066 §1.3.
/// </summary>
public sealed class HelmOptions
{
    /// <summary>
    /// Backstop periodic refresh for widgets that don't have reactive
    /// triggers. Default 1 minute. Per ADR 0066 §1.3 trigger #3.
    /// </summary>
    public TimeSpan PeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
}
