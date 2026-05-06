using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder.Widgets;

/// <summary>
/// Identity-glance Helm widget per ADR 0066 §1.4 (W#53 Phase 2 PR
/// 2a). Renders a top-of-Helm summary of the actor's identity
/// state with quick-access affordances for key rotation and
/// recovery-contact management.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase-2 placeholder per H7 halt-condition:</b>
/// <c>HistoricalKeysProjection</c> (ADR 0046-a1 Phase 1) is NOT yet
/// on origin/main. The widget ships with a deterministic
/// <see cref="SyncState.Stale"/> placeholder until the projection
/// lands; a follow-up PR will wire the real
/// <c>IIdentityGlanceProjection</c> read seam (read-only;
/// <c>IFieldDecryptor</c> MUST NOT be called from
/// <see cref="ComputeAsync"/> per ADR 0046-A2 + OQ-4 council
/// disposition — calling the audit-emitting decryptor per render
/// would generate spurious audit records).
/// </para>
/// <para>
/// <b>Slot:</b> <see cref="HelmSlot.GlanceBand"/>; OrderHint 100
/// (first widget in the GlanceBand row).
/// </para>
/// </remarks>
public sealed class IdentityGlanceWidget : IHelmWidget
{
    /// <inheritdoc />
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "identity-glance",
        Slot: HelmSlot.GlanceBand,
        OrderHint: 100,
        AccessibleName: "Identity glance",
        CapabilityGateType: null);

    /// <inheritdoc />
    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        // TODO(ADR 0046-a1): wire IIdentityGlanceProjection once
        // HistoricalKeysProjection lands on origin/main. The
        // projection MUST be read-only — never call IFieldDecryptor
        // from ComputeAsync (audit-emitting per ADR 0046-A2; the
        // periodic refresh tick would flood the audit trail).
        // SecondaryLabel is null until the localization surface picks
        // up the placeholder string per i18n cohort precedent. The
        // ambient SyncState.Stale signal already conveys the
        // placeholder semantics ("not yet wired") to the renderer.
        var state = new HelmWidgetViewState(
            State: SyncState.Stale,
            PrimaryLabel: "Identity",
            SecondaryLabel: null,
            Actions: new HelmWidgetAction[]
            {
                new(
                    ActionId: "rotate-key",
                    AccessibleLabel: "Rotate key",
                    Kind: HelmActionInvocationKind.Navigate,
                    Target: "wayfinder/identity/key-rotation"),
                new(
                    ActionId: "recovery-contacts",
                    AccessibleLabel: "Manage recovery contacts",
                    Kind: HelmActionInvocationKind.Navigate,
                    Target: "wayfinder/identity/recovery-contacts"),
            });
        return ValueTask.FromResult(state);
    }
}
