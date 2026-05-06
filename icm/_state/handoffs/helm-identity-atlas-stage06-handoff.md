# W#53 Stage 06 Hand-Off — Helm + Identity Atlas Surface (ADR 0066)

**Workstream:** W#53  
**ADR:** [0066 — Helm Composition + Identity Atlas Surface](../../../docs/adrs/0066-helm-composition-and-identity-atlas-surface.md)  
**Status at hand-off:** `design-in-flight` → flip to `ready-to-build` when this file merges  
**Package:** additive to `packages/ui-core/Wayfinder/` — **no new package**  
**Namespace:** `Sunfish.UICore.Wayfinder` (flat; sub-folders `Wayfinder/Identity/`,
`Wayfinder/Widgets/` for organisation only — single namespace per OQ-2 council decision)  
**Authored:** 2026-05-05 (XO research session)  
**Build estimate:** ~18-28h / ~5-6 PRs  
**Build phases:** Phase 1 (contract + KeyFingerprint) → Phase 2 (canonical Helm widgets +
adapter renderers); Phase 3 (identity Atlas implementations) is a **separate workstream**,
not scoped here  

---

## Critical context

W#53 is a **load-bearing prerequisite for W#48 Phase 1.** `IAtlasProvider<T>` — the
generic base that `IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>` extends
— is introduced by W#53 Phase 1. W#48 Phase 1 cannot compile until W#53 Phase 1 merges.

ADR 0066's own Phase 1 implementation checklist does **not** explicitly list `IAtlasProvider<T>`.
This hand-off adds it. Rationale: ADR 0066 was authored against the ADR 0067 spec text
simultaneously; `IAtlasProvider<T>` is the shared base that makes W#48's provider pattern
reusable across the four remaining Wayfinder Atlas surfaces (security policy, account-identity,
domain config, user preferences). It belongs in W#53 Phase 1, not in W#48.

---

## Prerequisites verification checklist (run before writing any code)

```bash
# H1: ADR 0065 substrate on origin/main
grep -rn "IStandingOrderIssuer" packages/foundation-wayfinder/ | head -3
# EXPECT: ≥1 hit (packages/foundation-wayfinder/IStandingOrderIssuer.cs)

# H2: MissionEnvelope on origin/main
grep -rn "namespace Sunfish.Foundation.MissionSpace" packages/foundation-mission-space/Models/MissionEnvelope.cs
# EXPECT: namespace Sunfish.Foundation.MissionSpace

# H3: IAtlasProvider<T> NOT yet in packages/ui-core/
grep -rn "IAtlasProvider" packages/ui-core/
# EXPECT: zero hits (this phase introduces it)

# H4: IHelmWidget NOT yet in packages/ui-core/
grep -rn "IHelmWidget" packages/ui-core/
# EXPECT: zero hits (this phase introduces it)

# H5: KeyFingerprint NOT yet in packages/foundation-recovery/
grep -rn "KeyFingerprint" packages/foundation-recovery/
# EXPECT: zero hits (this phase introduces it)
```

If H3, H4, or H5 return ≥1 hit, a parallel session may have landed these types already.
STOP, write `cob-question-[timestamp]-w53-symbols-already-present.md` to the research inbox,
and halt.

---

## Phase 1 — Contract surface + `KeyFingerprint` (~6-9h, ~2 PRs)

**Gate:** Prerequisites H1–H5 all pass.  
**No dependency on ADR 0046-a1 or `ICapabilityGate<T>`.** `HistoricalKeysProjection` is not
yet on `origin/main` (halt H7 governs — widget placeholder approach, Phase 2).
`HelmWidgetMetadata.CapabilityGateType` is `Type?` (CLR type reference), not
`ICapabilityGate<T>` directly; it compiles without `ICapabilityGate<T>` on origin/main.

### Phase 1a — `IAtlasProvider<T>` + Helm contract types (1 PR)

**Files to create:**

#### `packages/ui-core/Wayfinder/IAtlasProvider.cs`

```csharp
namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Generic base for Atlas provider specializations. Each Atlas sub-surface
/// (integration config, security policy, account identity, domain config, user
/// preferences) implements a typed specialization of this interface. Per ADR 0066 §1
/// and ADR 0067.
/// </summary>
/// <typeparam name="TView">
/// The projected view type returned by <see cref="GetAtlasViewAsync"/>. Must be a
/// reference type; the projection is always heap-allocated (contains collections).
/// </typeparam>
public interface IAtlasProvider<out TView>
    where TView : class
{
    /// <summary>
    /// Returns the current Atlas view for the ambient tenant/actor context.
    /// Implementations MUST be side-effect-free; projection only — no mutations,
    /// no audit emission, no Standing Order issuance.
    /// </summary>
    Task<TView> GetAtlasViewAsync(CancellationToken ct = default);
}
```

#### `packages/ui-core/Wayfinder/IHelmWidget.cs`

Full per ADR 0066 §1.1. Include all record types in the same file for namespace locality:

```csharp
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.UICore.Wayfinder;

public interface IHelmWidget
{
    HelmWidgetMetadata Metadata { get; }

    ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default);
}

public sealed record HelmWidgetMetadata(
    string WidgetId,
    HelmSlot Slot,
    int OrderHint,
    string AccessibleName,
    Type? CapabilityGateType);

public enum HelmSlot
{
    GlanceBand,
    ActionStack,
    ActivityFeed,
}

public sealed record HelmWidgetViewState(
    SyncState State,
    string PrimaryLabel,
    string? SecondaryLabel,
    IReadOnlyList<HelmWidgetAction> Actions);

public sealed record HelmWidgetAction(
    string ActionId,
    string AccessibleLabel,
    HelmActionInvocationKind Kind,
    string Target);

public enum HelmActionInvocationKind
{
    Navigate,
    IssueStandingOrder,
    RunLocalCommand,
}

public sealed record HelmRenderContext(
    MissionEnvelope Envelope,
    TenantId Tenant,
    ActorId Actor,
    TeamId? ActiveTeam,
    NodaTime.Instant Now);

public sealed class HelmOptions
{
    /// <summary>
    /// Backstop periodic refresh for widgets that don't have reactive triggers.
    /// Default: 1 minute. Range: [5s, 10min]. Per ADR 0066 §1.3 trigger #3.
    /// </summary>
    public TimeSpan PeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
}
```

**Note:** `TeamId` is in `Sunfish.Kernel.Runtime.Teams` (verified:
`packages/kernel-runtime/Teams/TeamId.cs`). Do NOT place it in any other namespace.
`MissionEnvelope` is in `Sunfish.Foundation.MissionSpace` (verified:
`packages/foundation-mission-space/Models/MissionEnvelope.cs`).
`TenantId` / `ActorId` are in `Sunfish.Foundation.Assets.Common` — NOT
`Sunfish.Foundation.Identity` (ADR 0065 has a stale cite; DO NOT propagate it here).

#### `packages/ui-core/Wayfinder/IHelmWidgetRegistry.cs`

```csharp
namespace Sunfish.UICore.Wayfinder;

public interface IHelmWidgetRegistry
{
    IReadOnlyList<IHelmWidget> Widgets { get; }
    IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot);
}
```

#### `packages/ui-core/Wayfinder/DefaultHelmWidgetRegistry.cs`

```csharp
namespace Sunfish.UICore.Wayfinder;

internal sealed class DefaultHelmWidgetRegistry : IHelmWidgetRegistry
{
    private readonly IReadOnlyList<IHelmWidget> _widgets;

    public DefaultHelmWidgetRegistry(IEnumerable<IHelmWidget> widgets)
    {
        _widgets = widgets
            .OrderBy(w => w.Metadata.Slot)
            .ThenBy(w => w.Metadata.OrderHint)
            .ToList();
    }

    public IReadOnlyList<IHelmWidget> Widgets => _widgets;

    public IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot) =>
        _widgets.Where(w => w.Metadata.Slot == slot).ToList();
}
```

#### `packages/ui-core/Wayfinder/HelmServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.UICore.Wayfinder;

public static class HelmServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishHelm(this IServiceCollection services)
        => services.AddSunfishHelm(_ => { });

    public static IServiceCollection AddSunfishHelm(
        this IServiceCollection services,
        Action<HelmOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<IHelmWidgetRegistry>(sp =>
            new DefaultHelmWidgetRegistry(sp.GetServices<IHelmWidget>()));
        return services;
    }

    public static IServiceCollection AddHelmWidget<TWidget>(
        this IServiceCollection services)
        where TWidget : class, IHelmWidget
        => services.AddSingleton<IHelmWidget, TWidget>();
}
```

### Phase 1b — `KeyFingerprint` + `IIdentityAtlasSurface` (1 PR)

#### `packages/foundation-recovery/KeyFingerprint.cs`

Additive to `packages/foundation-recovery/`. Namespace: `Sunfish.Foundation.Recovery`.

```csharp
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Canonical display form for a cryptographic key fingerprint. Hex SHA-256
/// of the public key, with ':' group-separators every 2 bytes —
/// 32 bytes × 2 hex chars + 31 separators = 95-char canonical string.
/// Per ADR 0066 §5 (OQ-6 council disposition: hex SHA-256 with ':' separators).
/// </summary>
[JsonConverter(typeof(KeyFingerprintJsonConverter))]
public readonly record struct KeyFingerprint(string Value) : IEquatable<KeyFingerprint>
{
    /// <summary>
    /// The 95-character canonical string: 64 hex chars + 31 ':' separators.
    /// </summary>
    public override string ToString() => Value;

    public static KeyFingerprint Parse(string value)
    {
        if (!IsValid(value))
            throw new FormatException(
                $"KeyFingerprint must be 95-char hex-with-colons; got '{value}'.");
        return new KeyFingerprint(value);
    }

    public static bool IsValid(string value)
    {
        if (value is null || value.Length != 95) return false;
        for (int i = 0; i < value.Length; i++)
        {
            if ((i + 1) % 3 == 0)
            {
                if (value[i] != ':') return false;
            }
            else
            {
                if (!Uri.IsHexDigit(value[i])) return false;
            }
        }
        return true;
    }
}

public sealed class KeyFingerprintJsonConverter : System.Text.Json.Serialization.JsonConverter<KeyFingerprint>
{
    public override KeyFingerprint Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
        => KeyFingerprint.Parse(reader.GetString()!);

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        KeyFingerprint value,
        System.Text.Json.JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
```

#### `packages/ui-core/Wayfinder/Identity/IIdentityAtlasSurface.cs`

New sub-folder `packages/ui-core/Wayfinder/Identity/`. Namespace stays
`Sunfish.UICore.Wayfinder` (flat per OQ-2 council decision) — the `Identity/` sub-folder
is organisation only.

```csharp
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.UICore.Wayfinder;

public interface IIdentityAtlasSurface
{
    ValueTask<IdentityProfileEditViewModel> GetProfileEditAsync(
        TenantId tenant, ActorId actor, CancellationToken ct);

    ValueTask<KeyRotationViewModel> GetKeyRotationAsync(
        TenantId tenant, ActorId actor, CancellationToken ct);

    ValueTask<RecoveryContactsViewModel> GetRecoveryContactsAsync(
        TenantId tenant, ActorId actor, CancellationToken ct);

    ValueTask<HistoricalKeysBrowseViewModel> GetHistoricalKeysAsync(
        TenantId tenant, ActorId actor, CancellationToken ct);

    ValueTask<ActiveTeamOverviewViewModel> GetActiveTeamOverviewAsync(
        TenantId tenant, ActorId actor, CancellationToken ct);
}
```

#### `packages/ui-core/Wayfinder/Identity/ViewModels.cs`

All view-model record types in one file per ADR 0066 §2.1-§2.6:

```csharp
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.UI;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.UICore.Wayfinder;

public sealed record IdentityProfileEditViewModel(
    ActorId Actor,
    string DisplayName,
    string ContactEmail,
    string? PhoneNumber);

public sealed record KeyRotationViewModel(
    ActorId Actor,
    KeyFingerprint CurrentFingerprint,
    int HistoricalKeyCount,
    bool RotationInProgress,
    NodaTime.Instant? RotationWindowExpiry);

public sealed record RecoveryContactsViewModel(
    ActorId Actor,
    IReadOnlyList<RecoveryContact> Contacts,
    int MaxContacts);

/// <summary>
/// User-facing UX term per OQ-1 council decision (ADR 0066 §A0, NM-1 disposition).
/// Audit vocabulary uses "Trustee" (per <c>AuditEventType.TrusteeSetChanged</c> in
/// ADR 0046); this type is the user-facing surface only.
/// </summary>
public sealed record RecoveryContact(
    ActorId ContactActorId,
    string DisplayName,
    SyncState VerificationStatus,
    NodaTime.Instant EnrolledAt);

public sealed record HistoricalKeysBrowseViewModel(
    ActorId Actor,
    IReadOnlyList<HistoricalKeyEntry> Keys);

public sealed record HistoricalKeyEntry(
    KeyFingerprint Fingerprint,
    NodaTime.Instant ActivatedAt,
    NodaTime.Instant? RetiredAt,
    string RotationReason,
    int SignatureSurvivalCount);

public sealed record ActiveTeamOverviewViewModel(
    ActorId Actor,
    IReadOnlyList<TeamMembershipEntry> Teams,
    TeamId? ActiveTeamId);

public sealed record TeamMembershipEntry(
    TeamId TeamId,
    string DisplayName,
    string RoleDisplayName,
    KeyFingerprint SubkeyFingerprint);
```

**Note on `HistoricalKeyEntry.RotationReason`:** typed as `string` not
`KeyRotationReason` enum because `KeyRotationReason` is introduced by ADR 0046-a1
which is NOT yet on `origin/main`. When ADR 0046-a1 Phase 1 lands, open a follow-up PR
to tighten the type to `KeyRotationReason`.

#### Phase 1 test file

`packages/ui-core/tests/WayfinderContractTests.cs`:

```csharp
namespace Sunfish.UICore.Tests;

public class WayfinderContractTests
{
    [Fact]
    public void HelmWidgetRegistry_OrdersBySlotThenOrderHint()
    {
        // arrange
        var widgets = new[] {
            MakeWidget("b", HelmSlot.GlanceBand, orderHint: 200),
            MakeWidget("a", HelmSlot.GlanceBand, orderHint: 100),
            MakeWidget("c", HelmSlot.ActionStack, orderHint: 100),
        };
        var registry = new DefaultHelmWidgetRegistry(widgets);

        // act
        var glance = registry.GetSlot(HelmSlot.GlanceBand);
        var action = registry.GetSlot(HelmSlot.ActionStack);

        // assert
        Assert.Equal(2, glance.Count);
        Assert.Equal("a", glance[0].Metadata.WidgetId); // OrderHint 100 first
        Assert.Equal("b", glance[1].Metadata.WidgetId); // OrderHint 200 second
        Assert.Single(action);
    }

    [Fact]
    public void HelmWidget_CapabilityGateType_NullMeansAlwaysShown()
    {
        var widget = MakeWidget("x", HelmSlot.GlanceBand, orderHint: 1);
        Assert.Null(widget.Metadata.CapabilityGateType);
    }

    [Fact]
    public void IAtlasProvider_IsCovariant()
    {
        // Compiler-only: verifies out TView covariance compiles
        IAtlasProvider<object> _ =
            (IAtlasProvider<object>)(object)new StubAtlasProvider();
    }

    [Fact]
    public void KeyFingerprint_RoundTrip()
    {
        var fp = new string('A', 2) + string.Join(":",
            Enumerable.Range(0, 32).Select(i => "AB"));
        // canonical: "AB:AB:AB:...:AB" = 95 chars
        var canonical = string.Join(":",
            Enumerable.Range(0, 32).Select(_ => "AB"));
        var parsed = KeyFingerprint.Parse(canonical);
        Assert.Equal(canonical, parsed.Value);
        Assert.Equal(canonical, parsed.ToString());
    }

    [Fact]
    public void KeyFingerprint_InvalidFormat_Throws()
    {
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse("not-a-fingerprint"));
    }

    private static IHelmWidget MakeWidget(string id, HelmSlot slot, int orderHint)
    {
        var meta = new HelmWidgetMetadata(id, slot, orderHint,
            AccessibleName: id, CapabilityGateType: null);
        var stub = Substitute.For<IHelmWidget>();
        stub.Metadata.Returns(meta);
        return stub;
    }

    private sealed class StubAtlasProvider : IAtlasProvider<object>
    {
        public Task<object> GetAtlasViewAsync(CancellationToken ct = default)
            => Task.FromResult(new object());
    }
}
```

`packages/foundation-recovery` test file — add to
`packages/foundation-recovery/tests/`:

```csharp
// KeyFingerprintTests.cs
public class KeyFingerprintTests
{
    private static string ValidFingerprint()
        => string.Join(":", Enumerable.Range(0, 32).Select(_ => "AB"));

    [Fact] public void Parse_Valid_Succeeds() =>
        Assert.Equal(ValidFingerprint(), KeyFingerprint.Parse(ValidFingerprint()).Value);

    [Fact] public void Parse_WrongLength_Throws() =>
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse("AB:CD"));

    [Fact] public void Parse_NoColons_Throws() =>
        Assert.Throws<FormatException>(() =>
            KeyFingerprint.Parse(new string('A', 64)));

    [Fact] public void JsonRoundTrip()
    {
        var fp = KeyFingerprint.Parse(ValidFingerprint());
        var json = System.Text.Json.JsonSerializer.Serialize(fp);
        var restored = System.Text.Json.JsonSerializer.Deserialize<KeyFingerprint>(json);
        Assert.Equal(fp, restored);
    }
}
```

### Phase 1 PR strategy

**PR 1a** — `IAtlasProvider<T>` + full Helm contract surface
(`packages/ui-core/Wayfinder/IAtlasProvider.cs`, `IHelmWidget.cs`,
`IHelmWidgetRegistry.cs`, `DefaultHelmWidgetRegistry.cs`,
`HelmServiceCollectionExtensions.cs`) + `WayfinderContractTests.cs` subset (all
tests except `KeyFingerprint` tests). Commit: `feat(ui-core): IAtlasProvider<T> +
IHelmWidget + IHelmWidgetRegistry contract surface (W#53 Phase 1a)`.

**PR 1b** — `KeyFingerprint` (additive to `packages/foundation-recovery/`) +
`IIdentityAtlasSurface` + all Identity view-model records +
`KeyFingerprintTests.cs`. Commit: `feat(foundation-recovery): KeyFingerprint value
type + IIdentityAtlasSurface contract (W#53 Phase 1b)`.

**Council gate:** BOTH PRs require standard adversarial + WCAG/a11y subagent council
pre-merge. Do NOT enable auto-merge before council returns. Per cohort batting average
(30-of-31 substrate amendments needed council fixes).

---

## Phase 2 — Canonical Helm widgets (~12-19h, ~3-4 PRs; gated on Phase 1 merged)

**Gate:** Phase 1 both PRs merged; `IHelmWidget` + `IAtlasProvider<T>` + `KeyFingerprint`
all on `origin/main`.

Six canonical widgets per ADR 0066 §1.4. Each in `packages/ui-core/Wayfinder/Widgets/`.

### Widget implementations

#### `IdentityGlanceWidget` (slot: GlanceBand, orderHint: 100)

```csharp
namespace Sunfish.UICore.Wayfinder;

public sealed class IdentityGlanceWidget : IHelmWidget
{
    public HelmWidgetMetadata Metadata { get; } = new(
        WidgetId: "identity-glance",
        Slot: HelmSlot.GlanceBand,
        OrderHint: 100,
        AccessibleName: "Identity glance",
        CapabilityGateType: null);

    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default)
    {
        // IMPORTANT: Do NOT call IFieldDecryptor from ComputeAsync.
        // IFieldDecryptor is audit-emitting per ADR 0046-A2; calling it
        // per render would generate spurious audit records. Use pre-computed
        // fingerprint projections only (per OQ-4 council disposition).
        //
        // HALT H7: HistoricalKeysProjection not yet on origin/main.
        // Ship with placeholder until ADR 0046-a1 Phase 1 lands.
        var state = new HelmWidgetViewState(
            State: SyncState.Healthy,
            PrimaryLabel: "Identity",
            SecondaryLabel: null,
            Actions: [
                new("rotate-key", "Rotate key", HelmActionInvocationKind.Navigate,
                    "wayfinder/identity/key-rotation"),
                new("recovery-contacts", "Manage recovery contacts",
                    HelmActionInvocationKind.Navigate,
                    "wayfinder/identity/recovery-contacts"),
            ]);
        return ValueTask.FromResult(state);
    }
}
```

**Note:** Implement a real `IdentityGlanceWidget` that queries a pre-computed
fingerprint projection (read-only, no `IFieldDecryptor`). The stub above shows
the structural contract only. Full implementation requires an `IIdentityGlanceProjection`
seam (or equivalent read-only query on the recovery substrate) — COB should design
this seam as part of Phase 2 and flag to research if a new ADR is needed.

#### `SyncStateWidget` (slot: GlanceBand, orderHint: 200)

Composes `SyncState` from `HelmRenderContext.Envelope`. Per ADR 0036 five-channel
encoding: `SyncState` value → label + icon hint. Widget renders the ambient
envelope sync state.

**Type note:** `MissionEnvelope.SyncState` is `SyncStateSnapshot` (record with `.State`,
`.LastSyncedAt`, `.ConflictCount`, `.ProbeStatus`). Unwrap to the `SyncState` enum
before calling `ToCanonicalIdentifier()` and before passing to `HelmWidgetViewState`.

```csharp
public sealed class SyncStateWidget : IHelmWidget
{
    public HelmWidgetMetadata Metadata { get; } = new(
        "sync-state", HelmSlot.GlanceBand, 200, "Sync state", null);

    public ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context, CancellationToken ct = default)
    {
        var snapshot = context.Envelope.SyncState;    // SyncStateSnapshot
        var syncState = snapshot.State;               // SyncState enum
        var label = syncState.ToCanonicalIdentifier(); // e.g., "healthy"
        return ValueTask.FromResult(new HelmWidgetViewState(
            syncState, label, null, []));
    }
}
```

#### `ActiveTeamWidget` (slot: GlanceBand, orderHint: 300)

Composes `HelmRenderContext.ActiveTeam`. If `null`, renders "No active team" label.

#### `MissionEnvelopeSummaryWidget` (slot: GlanceBand, orderHint: 400)

Composes top 3 capability-gate verdicts from `HelmRenderContext.Envelope`.
If no capabilities, renders "No capabilities active."

**Phase 2 PR 2a divergence (committed in PR #663):** the dimension-coverage
rendering ships in lieu of "top 3 capability-gate verdicts" because
`ICapabilityGate<T>` is not on origin/main (per ADR 0066 §1.1
`HelmWidgetMetadata.CapabilityGateType` rationale — the `Type`-rather-than-
generic-constraint shape exists precisely because the gate type isn't yet
shipped). The widget counts the 10 ADR 0062-A1.2 envelope dimensions:
`SyncState.Healthy` when all present + "10 dimensions active" secondary,
`SyncState.Stale` + view-envelope Navigate when partial. A follow-up
amendment will expand to per-feature-gate verdicts via an
`IFeatureVerdictProvider` seam when the gate type lands. This is a
Phase-2-aware substitution, not a contract change — the slot, OrderHint,
and `HelmWidgetViewState` shape are unchanged.

#### `QuickTogglesWidget` (slot: ActionStack, orderHint: 100)

Composes `IStandingOrderIssuer` via three toggle actions:
- Offline mode: `Path = "system.network.offline"`, `Scope = StandingOrderScope.Platform`
- DND mode: `Path = "system.notifications.dnd"`, `Scope = StandingOrderScope.User`
- Pause sync: `Path = "system.sync.paused"`, `Scope = StandingOrderScope.Platform`

**Scope note:** `StandingOrderScope.System` does not exist. The live enum values are
`User`, `Tenant`, `Platform`, `Integration`, `Security` (see
`packages/foundation-wayfinder/StandingOrderScope.cs`). Network/sync-wide toggles use
`Platform` (spans tenants on the local node); per-user preferences use `User`.

Each action is `HelmActionInvocationKind.IssueStandingOrder`. Per H8 below, reactive
post-issuance refresh falls back to periodic refresh until ADR 0065-A1 ships.

**Important:** `QuickTogglesWidget` MUST NOT call `IStandingOrderIssuer.IssueAsync`
from `ComputeAsync`. `ComputeAsync` is read-only. The toggle actions are surfaced
as `HelmWidgetAction` entries with `Kind = IssueStandingOrder`; the adapter renderer
is responsible for calling the issuer when the user activates the toggle.

#### `RecentStandingOrdersWidget` (slot: ActivityFeed, orderHint: 100)

Composes `IStandingOrderRepository` query for the 5 most recent orders for the
ambient actor. Per H8, reactive subscription falls back to periodic refresh.

### Phase 2 adapter renderers

**Blazor:** `packages/ui-adapters-blazor/Wayfinder/HelmRenderer.razor`
— renders `IHelmWidgetRegistry.GetSlot(HelmSlot.GlanceBand)` + `GetSlot(ActionStack)`
+ `GetSlot(ActivityFeed)`. Each widget rendered in a region with:
- `aria-label` = `widget.Metadata.AccessibleName` (WCAG 4.1.2)
- `aria-live="polite"` on SyncState widget region (WCAG 4.1.3)
- Keyboard-only navigation between widgets via standard tabindex flow

**React:** `packages/ui-adapters-react/Wayfinder/HelmRenderer.tsx`
— equivalent parity. Adapter parity gate must pass before Phase 2 closes.

### Phase 2 WCAG test table

| SC | Requirement | Test type | Pass criteria |
|---|---|---|---|
| 4.1.2 | All widgets have `aria-label` = `AccessibleName` | Automated | `SunfishA11yAssertions.AssertHasAccessibleName` on every rendered widget |
| 4.1.3 | SyncState transitions fire `aria-live="polite"` | Automated | Live region present in DOM; polite (not assertive) |
| 1.4.11 | `KeyFingerprint` rendered in ≥3:1 contrast against background | Automated + visual | Contrast check in both adapters |
| 1.3.3 | SyncState NOT conveyed by colour alone | Manual | Icon + label + colour (per ADR 0036 five-channel) |
| 2.5.5 | QuickToggles touch targets ≥44×44 CSS px | Manual | Visual inspection in Anchor + Bridge |
| 2.4.6 | Helm pane has descriptive heading | Automated | H2 or `aria-label` on Helm pane container |

### Phase 2 halt conditions

- **H7:** `HistoricalKeysProjection` (ADR 0046-a1) NOT on `origin/main` (verified
  pre-build: `grep -rn "HistoricalKeysProjection" packages/` returns zero). Ship
  `IdentityGlanceWidget` with placeholder `SyncState.Stale + "Historical keys:
  loading…"` view-state. Queue a follow-up PR comment "TODO: tighten
  IdentityGlanceWidget once ADR 0046-a1 Phase 1 lands." Do NOT block Phase 2
  on this.

- **H8:** `IObservable<StandingOrderAppliedEvent>` NOT on `origin/main` (council
  confirmed at ADR 0066 §A0: `grep -rn "IObservable\|StandingOrderApplied"
  packages/foundation-wayfinder/` returns zero). Widgets that depend on
  reactive Standing Order propagation (`QuickTogglesWidget`,
  `RecentStandingOrdersWidget`) fall back to `HelmOptions.PeriodicRefreshInterval`
  only. Do NOT introduce an `IObservable` dependency in Phase 2; leave a
  `// TODO(ADR 0065-A1): wire reactive propagation when StandingOrderAppliedEvent
  ships` comment in the widget's `ComputeAsync` body.

- **H9 (adapter parity gate):** Both Blazor and React `HelmRenderer` must pass the
  shared parity test suite before Phase 2 PR closes. Per `_shared/engineering/
  coding-standards.md` parity requirements.

### Phase 2 PR strategy

**PR 2a** — `IdentityGlanceWidget` + `SyncStateWidget` + `ActiveTeamWidget`
+ `MissionEnvelopeSummaryWidget`.  
**PR 2b** — `QuickTogglesWidget` + `RecentStandingOrdersWidget`.  
**PR 2c** — Blazor `HelmRenderer.razor` + React `HelmRenderer.tsx` + WCAG tests
+ parity tests.  
**Optional PR 2d** — if `apps/kitchen-sink` wiring is separable.

Council gate: standard adversarial + WCAG/a11y subagent pre-merge on PR 2c
(UI-bearing phase; mandatory per ADR 0065 §7 + W#34 §5.7).

---

## Phase 3 — Identity Atlas implementations (DEFERRED — separate workstream)

Phase 3 (per ADR 0066 §"Phase 3 — identity Atlas surface") implements
`IIdentityAtlasSurface` for Anchor and Bridge accelerators. This is **NOT part of
W#53** — it is a separate workstream to be filed when W#53 Phase 1 merges and the
accelerator team is ready to implement the five Atlas pages.

XO will author a W#54 (or equivalent) hand-off when the accelerator implementations
are prioritised. COB should NOT begin Phase 3 without a dedicated hand-off file.

---

## Halt conditions summary

| ID | Condition | Action on failure |
|---|---|---|
| H1 | `IStandingOrderIssuer` on origin/main | Halt, flag to research |
| H2 | `MissionEnvelope` on origin/main | Halt, flag to research |
| H3 | `IAtlasProvider<T>` NOT yet in ui-core | If present: stop, cob-question |
| H4 | `IHelmWidget` NOT yet in ui-core | If present: stop, cob-question |
| H5 | `KeyFingerprint` NOT yet in foundation-recovery | If present: stop, cob-question |
| H6 | Council pre-merge canonical | Mandatory; no auto-merge before verdict |
| H7 | `HistoricalKeysProjection` absent → placeholder approach | Proceed with placeholder; queue follow-up |
| H8 | `IObservable<StandingOrderAppliedEvent>` absent → periodic fallback | Proceed with fallback; leave TODO comment |
| H9 | Adapter parity gate | Phase 2 cannot close until both adapters pass |

---

## Acceptance criteria

### Phase 1

- [ ] `IAtlasProvider<T>` in `packages/ui-core/Wayfinder/IAtlasProvider.cs`,
  namespace `Sunfish.UICore.Wayfinder`
- [ ] `IHelmWidget` + all associated record types in `packages/ui-core/Wayfinder/`
- [ ] `IHelmWidgetRegistry` + `DefaultHelmWidgetRegistry` in `packages/ui-core/Wayfinder/`
- [ ] `HelmServiceCollectionExtensions` with two-overload `AddSunfishHelm` +
  `AddHelmWidget<T>` in `packages/ui-core/Wayfinder/`
- [ ] `HelmOptions` configurable via `IOptions<HelmOptions>` wired by `AddSunfishHelm`
- [ ] `KeyFingerprint` + `KeyFingerprintJsonConverter` in `packages/foundation-recovery/`
- [ ] `IIdentityAtlasSurface` + all view-model records in `packages/ui-core/Wayfinder/Identity/`
- [ ] `RecoveryContact` typed with user-facing UX vocabulary (NOT `Trustee` —
  per OQ-1 council NM-1 disposition)
- [ ] `WayfinderContractTests.cs` passes: slot-ordering, `OrderHint` stable-sort,
  `IAtlasProvider<T>` covariance compiler check
- [ ] `KeyFingerprintTests.cs` passes: parse, invalid-format throw, JSON round-trip
- [ ] XML doc on all public types (one-line minimum per cohort doc discipline)
- [ ] `grep -rn "IAtlasProvider" packages/ui-core/` returns ≥1 match
  (unblocks W#48 Phase 1 gate)
- [ ] Council verdict: LGTM or mechanical amendments applied before auto-merge

### Phase 2

- [ ] All 6 canonical widgets implemented in `packages/ui-core/Wayfinder/Widgets/`
- [ ] `IdentityGlanceWidget` ships with placeholder if `HistoricalKeysProjection`
  absent; TODO comment left for ADR 0046-a1 follow-up
- [ ] `QuickTogglesWidget` + `RecentStandingOrdersWidget` use periodic-refresh fallback
  with `// TODO(ADR 0065-A1)` comment
- [ ] Blazor `HelmRenderer.razor` + React `HelmRenderer.tsx` in respective adapters
- [ ] Parity test suite passes both adapters
- [ ] WCAG test table (Phase 2 section above) all rows pass
- [ ] Council verdict: LGTM or amendments applied before PR 2c auto-merge

---

## Open questions resolved by this hand-off

| OQ | Resolution |
|---|---|
| OQ-1 (RecoveryContact vs Trustee) | `RecoveryContact` user-facing; `Trustee` audit-vocabulary (per council NM-1) |
| OQ-2 (flat vs split namespace) | Flat `Sunfish.UICore.Wayfinder` with sub-folders (per council NM-3) |
| OQ-3 (IObservable dependency) | Phase 2 falls back to periodic-refresh; TODO comment for ADR 0065-A1 (per H8) |
| OQ-4 (IFieldDecryptor from ComputeAsync) | FORBIDDEN — audit-emitting; fingerprint is pre-computed projection only |
| OQ-5 (Bridge admin Helm subset) | Deferred — Phase 3 ships per-tenant identity Atlas only for self-service Bridge users |
| OQ-6 (KeyFingerprint canonical form) | Hex SHA-256 + ':' separators every 2 bytes; 95-char canonical string |

---

## Files to create / modify

| File | Action |
|---|---|
| `packages/ui-core/Wayfinder/IAtlasProvider.cs` | CREATE |
| `packages/ui-core/Wayfinder/IHelmWidget.cs` | CREATE |
| `packages/ui-core/Wayfinder/IHelmWidgetRegistry.cs` | CREATE |
| `packages/ui-core/Wayfinder/DefaultHelmWidgetRegistry.cs` | CREATE |
| `packages/ui-core/Wayfinder/HelmServiceCollectionExtensions.cs` | CREATE |
| `packages/ui-core/Wayfinder/Identity/IIdentityAtlasSurface.cs` | CREATE |
| `packages/ui-core/Wayfinder/Identity/ViewModels.cs` | CREATE |
| `packages/ui-core/tests/WayfinderContractTests.cs` | CREATE |
| `packages/foundation-recovery/KeyFingerprint.cs` | CREATE |
| `packages/foundation-recovery/tests/KeyFingerprintTests.cs` | CREATE |
| `packages/ui-core/Wayfinder/Widgets/IdentityGlanceWidget.cs` | CREATE (Phase 2) |
| `packages/ui-core/Wayfinder/Widgets/SyncStateWidget.cs` | CREATE (Phase 2) |
| `packages/ui-core/Wayfinder/Widgets/ActiveTeamWidget.cs` | CREATE (Phase 2) |
| `packages/ui-core/Wayfinder/Widgets/MissionEnvelopeSummaryWidget.cs` | CREATE (Phase 2) |
| `packages/ui-core/Wayfinder/Widgets/QuickTogglesWidget.cs` | CREATE (Phase 2) |
| `packages/ui-core/Wayfinder/Widgets/RecentStandingOrdersWidget.cs` | CREATE (Phase 2) |
| `packages/ui-adapters-blazor/Wayfinder/HelmRenderer.razor` | CREATE (Phase 2) |
| `packages/ui-adapters-react/Wayfinder/HelmRenderer.tsx` | CREATE (Phase 2) |
| `icm/_state/active-workstreams.md` | UPDATE W#53 row `design-in-flight` → `ready-to-build` |
