# W#58 Stage 06 Hand-off: Identity Atlas Implementations (Anchor + Bridge)

**Authored:** 2026-05-06  
**Workstream:** W#58 — ADR 0066 §Phase 3 deferred (W#53 hand-off: "COB should NOT
begin Phase 3 without a dedicated hand-off file")  
**Pipeline variant:** sunfish-feature-change  
**Effort estimate:** ~27h / 4 phases / ~6 PRs

---

## Critical context

`IIdentityAtlasSurface` is a **READ-ONLY projection surface**. Implementations:
- MUST NOT call `IStandingOrderIssuer`
- MUST NOT call `IAuditTrail.AppendAsync`
- MUST NOT call `IFieldDecryptor` (audit-emitting per ADR 0046-A2; forbidden in
  projection paths per ADR 0066 OQ-4 council decision)
- MUST return view-model projections assembled from existing read-side state

The five Atlas pages render these view-models. Mutation actions (profile edit, key
rotation, recovery-contact enroll/remove, active-team switch) are Standing Order
issuances wired separately via the Helm action dispatch path. This hand-off covers
ONLY the projection side.

**`ActiveTeamOverviewViewModel.ActiveTeamId`** uses `Guid` (not `TeamId` from
`Sunfish.Kernel.Runtime.Teams`) per the W#53 P1a cycle-break decision. Anchor wraps
the `Guid` back to `TeamId` at the boundary; Bridge uses the `Guid` directly (no
kernel-runtime reference in Bridge.Client).

---

## Halt conditions

| ID | Condition | Verification command | Gates |
|---|---|---|---|
| **H1** | W#53 Phase 2 complete — React adapter PR 2d merged | `grep -r "HelmRenderer\|QuickTogglesWidget\|RecentStandingOrdersWidget" packages/ui-adapters-react/` | Phase 1 start |
| **H2** | `IIdentityAtlasSurface` + ViewModels.cs on origin/main | `grep -l "IIdentityAtlasSurface" packages/ui-core/` | CLEARED ✓ (W#53 P1a) |
| **H3** | `IDiffPreview` + `DiffPreviewView` in UICore | `grep -rl "IDiffPreview" packages/ui-core/` | CLEARED ✓ (W#46 P3) |
| **H4** | ADR 0046-A1 (`HistoricalKeysProjection`) on origin/main | `grep "Status: Accepted" docs/adrs/0046-a1*.md` | Phase 1b follow-up only; Phase 1 ships placeholder |
| **H5** | `KeyFingerprint` in `packages/foundation/Crypto/` | `find packages/foundation/Crypto -name "KeyFingerprint*"` | CLEARED ✓ (W#53 P1b PR #633) |
| **H6** | Council pre-merge canonical | Run council before every UI-bearing phase | Mandatory — no skip |

**NuGet binary halt:** `find packages/ -name "*.nupkg" | wc -l` must be 0 before Phase 1.

---

## Prerequisites verification checklist (run before writing any code)

```bash
# H1 — W#53 Phase 2 closed (React adapter)
grep -rl "HelmRenderer\|SunfishHelm" packages/ui-adapters-react/src/ 2>/dev/null \
  && echo "H1 CLEARED" || echo "H1 BLOCKED — wait for W#53 PR 2d"

# H2 — IIdentityAtlasSurface contracts
grep -l "IIdentityAtlasSurface" packages/ui-core/Wayfinder/Identity/ \
  && echo "H2 CLEARED" || echo "H2 BLOCKED"

# H3 — IDiffPreview + DiffPreviewView
grep -rl "IDiffPreview\b" packages/ui-core/ \
  && echo "H3 CLEARED" || echo "H3 BLOCKED"

# H5 — KeyFingerprint
find packages/foundation/Crypto -name "KeyFingerprint*" \
  && echo "H5 CLEARED" || echo "H5 BLOCKED"

# Negative-existence: AnchorIdentityAtlasSurface not yet present
grep -rl "AnchorIdentityAtlasSurface" accelerators/anchor/ 2>/dev/null \
  && echo "STOP: parallel session may have started Phase 1" || echo "CLEAR to begin"
```

---

## Phase 1 — `AnchorIdentityAtlasSurface` + 5 Anchor Blazor pages

**Gate:** H1 (W#53 Phase 2 complete)  
**Effort:** ~10h / 2 PRs (Phase 1a infrastructure; Phase 1b pages)  
**Council:** WCAG/a11y subagent mandatory before merge

### Phase 1a: `AnchorIdentityAtlasSurface` implementation (~4h)

**New file:** `accelerators/anchor/Services/AnchorIdentityAtlasSurface.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security;
using Sunfish.UICore.Wayfinder;

namespace Sunfish.Anchor.Services;

public sealed class AnchorIdentityAtlasSurface : IIdentityAtlasSurface
{
    private readonly IKeyStore _keyStore;
    private readonly ITrusteeRegistry _trusteeRegistry;
    private readonly IActiveTeamAccessor _activeTeam;
    private readonly ITeamRegistry _teamRegistry;

    public AnchorIdentityAtlasSurface(
        IKeyStore keyStore,
        ITrusteeRegistry trusteeRegistry,
        IActiveTeamAccessor activeTeam,
        ITeamRegistry teamRegistry)
    {
        _keyStore = keyStore;
        _trusteeRegistry = trusteeRegistry;
        _activeTeam = activeTeam;
        _teamRegistry = teamRegistry;
    }

    public async ValueTask<IdentityProfileEditViewModel> GetProfileEditAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        // Read display name + contact info from kernel-security identity store
        // No IFieldDecryptor call (plain-text projection only per OQ-4)
        var profile = await _keyStore.GetIdentityProfileAsync(tenant, actor, ct);
        return new IdentityProfileEditViewModel(
            actor,
            profile.DisplayName,
            profile.ContactEmail,
            profile.PhoneNumber);
    }

    public async ValueTask<KeyRotationViewModel> GetKeyRotationAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var keyInfo = await _keyStore.GetCurrentKeyInfoAsync(tenant, actor, ct);
        var fingerprint = KeyFingerprint.FromPublicKey(keyInfo.PublicKey);
        return new KeyRotationViewModel(
            actor,
            fingerprint,
            keyInfo.HistoricalKeyCount,
            keyInfo.RotationInProgress,
            keyInfo.RotationWindowExpiry);
    }

    public async ValueTask<RecoveryContactsViewModel> GetRecoveryContactsAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var policy = await _trusteeRegistry.GetPolicyAsync(tenant, ct);
        var trustees = await _trusteeRegistry.GetTrusteesAsync(tenant, actor, ct);
        var contacts = trustees
            .Select(t => new RecoveryContact(
                t.TrusteeActorId,
                t.DisplayName,
                t.VerificationState,
                t.EnrolledAt))
            .ToList();
        return new RecoveryContactsViewModel(actor, contacts, policy.MaxTrustees);
    }

    public ValueTask<HistoricalKeysBrowseViewModel> GetHistoricalKeysAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        // H4 PLACEHOLDER: ADR 0046-A1 HistoricalKeysProjection not yet on main.
        // Ships empty list; Phase 1b follow-up populates from projection when
        // ADR 0046-A1 is Accepted and shipped.
        return ValueTask.FromResult(
            new HistoricalKeysBrowseViewModel(actor, Array.Empty<HistoricalKeyEntry>()));
    }

    public async ValueTask<ActiveTeamOverviewViewModel> GetActiveTeamOverviewAsync(
        TenantId tenant, ActorId actor, CancellationToken ct = default)
    {
        var memberships = await _teamRegistry.GetMembershipsAsync(actor, ct);
        var activeTeam = _activeTeam.ActiveTeamId; // Guid? per cycle-break
        var entries = memberships
            .Select(m => new TeamMembershipEntry(
                m.TeamId.Value,          // Guid from TeamId
                m.DisplayName,
                m.RoleDisplayName,
                m.SubkeyFingerprint))
            .ToList();
        return new ActiveTeamOverviewViewModel(actor, entries, activeTeam);
    }
}
```

**DI registration:** Add to `accelerators/anchor/MauiProgram.cs`:
```csharp
builder.Services.AddSingleton<IIdentityAtlasSurface, AnchorIdentityAtlasSurface>();
```

**Tests (Phase 1a, ≥6 tests):**
- `GetProfileEditAsync_ReturnsMappedViewModel`
- `GetKeyRotationAsync_ReturnsFingerprintAndRotationState`
- `GetRecoveryContactsAsync_MapsAllTrustees`
- `GetHistoricalKeysAsync_ReturnsEmptyListWhileH4Pending`
- `GetActiveTeamOverviewAsync_MapsAllMemberships`
- `AnchorIdentityAtlasSurface_DoesNotDependOnFieldDecryptor` (reflection test)

### Phase 1b: 5 Anchor Blazor identity pages (~6h)

**New files in `accelerators/anchor/Components/Pages/Identity/`:**

```
IdentityProfileEditPage.razor      @page "/identity/profile"
KeyRotationPage.razor              @page "/identity/keys"
RecoveryContactsPage.razor         @page "/identity/recovery"
HistoricalKeysPage.razor           @page "/identity/keys/history"
ActiveTeamOverviewPage.razor       @page "/identity/teams"
```

**Common pattern per page (e.g., `KeyRotationPage.razor`):**
```razor
@page "/identity/keys"
@using Sunfish.UICore.Wayfinder
@inject IIdentityAtlasSurface AtlasSurface
@inject ICurrentActorProvider CurrentActor
@inject ITenantContextProvider TenantContext

<PageTitle>Sunfish — Key Management</PageTitle>

<main role="main" aria-labelledby="page-heading">
    <h1 id="page-heading">Key Management</h1>
    @if (_viewModel is not null)
    {
        <!-- Render KeyRotationViewModel fields -->
        <!-- IDiffPreview wiring for rotation confirmation (Phase 4) -->
    }
    else
    {
        <p aria-live="polite">Loading…</p>
    }
</main>

@code {
    private KeyRotationViewModel? _viewModel;

    protected override async Task OnInitializedAsync()
    {
        _viewModel = await AtlasSurface.GetKeyRotationAsync(
            TenantContext.CurrentTenantId,
            CurrentActor.CurrentActorId);
    }
}
```

**WCAG requirements per page (non-negotiable):**
- SC 2.4.6: descriptive H1 per page; sub-sections H2/H3 with `aria-labelledby`
- SC 3.3.7: Redundant Entry — within a recovery-contacts session, do NOT re-ask data already supplied
- SC 3.3.8 / 3.3.9: recovery-contact verification MUST NOT use cognitive-recall challenges
- SC 4.1.3: sync-state `aria-live="polite"`; compromise events `role=alert`

**Nav wiring:** Add routes to `NavMenu.razor` under a "My Identity" section.

**Phase 1b acceptance criteria:**
- All 5 pages render without errors
- `HistoricalKeysPage` shows empty state with clear "History not yet available" message
- WCAG/a11y subagent returns PASS (or MECHANICAL-ONLY amendments pre-applied)

---

## Phase 2 — `BridgeIdentityAtlasSurface` + 5 Bridge Blazor pages

**Gate:** Phase 1 complete  
**Effort:** ~8h / 2 PRs  
**Council:** WCAG/a11y + security-engineering subagents mandatory

### Phase 2a: Bridge .csproj + `BridgeIdentityAtlasSurface` (~4h)

**`accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj`** — add:
```xml
<ProjectReference Include="..\..\..\packages\foundation-recovery\Sunfish.Foundation.Recovery.csproj" />
```

**New file:** `accelerators/bridge/Sunfish.Bridge/Features/Identity/BridgeIdentityAtlasSurface.cs`

Bridge implementation is a **hosted-tenant subset** per ADR 0066 §Phase 3:
- Bridge admin users operate on THEIR OWN hosted-tenant identity surface
- Multi-tenant context: every read must be scoped to the requesting TenantId + ActorId
- No team-switching mutation from the Atlas surface (Bridge uses session-based team context)

```csharp
namespace Sunfish.Bridge.Features.Identity;

public sealed class BridgeIdentityAtlasSurface : IIdentityAtlasSurface
{
    // Similar to Anchor but reads from Bridge.Data repositories
    // Uses IHttpContextAccessor for current TenantId/ActorId rather than
    // injected IActiveTeamAccessor (which is Anchor-specific)
    ...
}
```

**Security council requirement for Phase 2a:**
- Verify tenant isolation: `GetRecoveryContactsAsync(tenantId, actorId)` MUST return only
  contacts enrolled under `tenantId`; must not cross tenant boundaries
- IFieldDecryptor absence check (reflection test — mandatory per ADR 0066 OQ-4)

### Phase 2b: 5 Bridge Blazor pages (~4h)

**New files in `accelerators/bridge/Sunfish.Bridge/Features/Identity/Pages/`:**

```
IdentityProfileEditPage.razor      @page "/identity/profile"
KeyRotationPage.razor              @page "/identity/keys"
RecoveryContactsPage.razor         @page "/identity/recovery"
HistoricalKeysPage.razor           @page "/identity/keys/history"
ActiveTeamOverviewPage.razor       @page "/identity/teams"
```

Bridge pages use `IHttpContextAccessor` to resolve TenantId/ActorId rather than
`ITenantContextProvider` (which is Anchor-specific).

---

## Phase 3 — Bridge React adapter parity

**Gate:** Phase 2 complete  
**Effort:** ~6h / 1 PR  
**Council:** WCAG/a11y subagent mandatory (adapter parity gate per ADR 0014)

**New files in `accelerators/bridge/Sunfish.Bridge.Client/src/pages/Identity/`:**

```
IdentityProfileEditPage.tsx
KeyRotationPage.tsx
RecoveryContactsPage.tsx
HistoricalKeysPage.tsx
ActiveTeamOverviewPage.tsx
```

TypeScript projections: Bridge exposes a JSON endpoint per identity page (similar to
the SystemRequirements endpoint pattern in W#56); React components fetch and render.

**New Bridge endpoint:** `accelerators/bridge/Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs`
```csharp
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/identity/profile", ...).RequireAuthorization();
        app.MapGet("/api/v1/identity/keys", ...).RequireAuthorization();
        app.MapGet("/api/v1/identity/recovery", ...).RequireAuthorization();
        app.MapGet("/api/v1/identity/keys/history", ...).RequireAuthorization();
        app.MapGet("/api/v1/identity/teams", ...).RequireAuthorization();
        return app;
    }
}
```

**Adapter parity matrix update:** add W#58 row to `_shared/engineering/adapter-parity.md`.

---

## Phase 4 — Diff-preview wiring + docs + ledger close

**Gate:** Phase 3 complete  
**Effort:** ~3h / 1 PR

### Diff-preview wiring

For Standing Order issuance on identity mutations (profile edit, key rotation,
recovery-contact enroll/remove), the Helm action dispatch renders a diff-preview
BEFORE the user confirms the Standing Order. This phase wires the `IDiffPreview`
surface into each mutation page.

**Not in Phase 1/2/3:** diff-preview rendering requires the Standing Order flow
to be wired up per the Helm widget action dispatch. Phase 4 adds the confirmation
dialog + `DiffPreviewView.Expanded` rendering on each identity page.

### Documentation

**New file:** `apps/docs/wcag/identity-atlas.md`
```markdown
# Identity Atlas WCAG Conformance

Documents WCAG 2.2 AA conformance for the five identity Atlas pages.
Per ADR 0066 §3, SC 3.3.7 / 3.3.8 / 3.3.9 apply to recovery-contact
enrollment and key-rotation flows.

<!-- Auto-updated per release; see ADR 0066 §apps/docs obligation -->
```

**Ledger update:** flip W#58 to `built`.

---

## Acceptance criteria (all phases)

### FAIL conditions (STOP on any)
- Any call to `IFieldDecryptor` inside `AnchorIdentityAtlasSurface` or `BridgeIdentityAtlasSurface`
- Any direct audit emission (`IAuditTrail.AppendAsync`) inside the surface implementations
- Any Standing Order issuance inside the surface implementations
- Bridge `BridgeIdentityAtlasSurface` returning contacts/keys from a different tenant
- WCAG/a11y subagent returns Critical findings

### PASS conditions
- All 5 Anchor pages render from `AnchorIdentityAtlasSurface` projections
- All 5 Bridge Blazor pages render from `BridgeIdentityAtlasSurface` projections
- All 5 Bridge React pages render from JSON endpoints
- `HistoricalKeysPage` shows informative empty state (H4 placeholder accepted)
- Adapter parity matrix updated
- WCAG/a11y subagent returns PASS or MECHANICAL-ONLY
- Security subagent confirms tenant-isolation + no IFieldDecryptor

---

## Key type surface (§A0 self-audit)

All types below verified to exist on `origin/main` before this hand-off was authored:

| Symbol | Package | Status |
|---|---|---|
| `IIdentityAtlasSurface` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `IdentityProfileEditViewModel` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `KeyRotationViewModel` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `RecoveryContactsViewModel` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `HistoricalKeysBrowseViewModel` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `ActiveTeamOverviewViewModel` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `RecoveryContact` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `TeamMembershipEntry` | `Sunfish.UICore.Wayfinder` | VERIFIED ✓ |
| `KeyFingerprint` | `Sunfish.Foundation.Crypto` | VERIFIED ✓ |
| `IDiffPreview` + `DiffPreviewView` | `Sunfish.UICore.Primitives` | VERIFIED ✓ |
| `ILiveAnnouncer` | `Sunfish.UICore.Primitives` | VERIFIED ✓ |

**Negative-existence (not yet on origin/main — placeholders required):**
| Symbol | Reason | Placeholder approach |
|---|---|---|
| `HistoricalKeysProjection` | ADR 0046-A1 Proposed | Empty `Keys` list + comment in Phase 1 |
