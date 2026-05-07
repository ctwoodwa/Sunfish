# Workstream #59 — Crew Comms Anchor MVP Demo Integration — Stage 06 hand-off

**Workstream:** #59 — Crew Comms Anchor MVP (LAN demo integration on top of W#45 substrate)
**Spec:** [ADR 0076](../../docs/adrs/0076-crew-comms-foundation-channels.md) (Accepted; A1/A2/A3 all landed)
**Pipeline variant:** `sunfish-feature-change` (consumer wiring + new UI surface; no contract changes)
**Estimated effort:** 10–13 hours focused sunfish-PM time
**Decomposition:** 6 phases shipping as ~5 PRs
**Pre-merge council:** Phase 4 (UI; WCAG/a11y subagent canonical per ADR 0065 §7) + Phase 5 (Anchor consumer page; standard 4-perspective). Phases 1–3 + 6: standard review.

**Authored:** 2026-05-06 by XO research session.

---

## Context

W#45 shipped the Crew Comms substrate (`foundation-channels` contracts + `blocks-crew-comms` native impl with Protocol/Crypto/Session/Presence/Signaling/NativeChannelProvider/DI). W#30 shipped `foundation-transport` with `ITransportSelector` + `MdnsPeerTransport` + `BridgeRelayPeerTransport` + `AddSunfishTransport()` DI extension. ADR 0076 + A1 + A2 + A3 conformance vectors are all Accepted.

What's missing for a live demo: (1) Anchor doesn't register transport; (2) `ICrewRoster` is empty; (3) no `ListenAsync` driver; (4) no UI. This hand-off closes those four gaps for a Mac↔Mac LAN-mDNS demo.

**Demo target (final acceptance):**
- Two macOS Anchor instances on the same Wi-Fi network
- Each has completed QR-pairing into the same team (existing `QrOnboardingService` flow)
- After pairing, each peer appears in the other's presence list (online dot)
- Either peer can open a chat session via the Sunfish UI; receiver gets an in-app invitation prompt
- Plain-text messages flow both directions with scrollback
- Closing one app moves its presence dot to offline within 45s (ADR 0076 presence TTL)
- Cross-relay (Bridge) demo path is **out of scope** for this workstream — LAN-mDNS only

**Scope explicitly NOT in this hand-off** (keep small, demo-able):
- TYPING / DELIVERED indicators (P4.5 PR 2 + PR 3 — separate workstream; nice-to-have post-demo)
- Transcript-hash A1/A2 alignment (P4.5 PR 1 — security; only required for cross-relay path which we're not demoing)
- Glare-wiring (P4.5 — only matters under concurrent OpenAsync; demo flow is single-initiator)
- Persistent `KeyPair` across app restart (Phase-1 stub fresh-keygens; ephemeral identity OK for live demo)
- Audio (Phase 3) and video (Phase 4) — never in scope
- React parity for `SunfishChat` — explicitly deferred per CO directive 2026-05-06 ("crew-comms MVP priority over React adapters this week"). Adapter parity follow-on filed as separate workstream once demo lands.

**Hard prerequisites (verified on origin/main 2026-05-06):**
- `Sunfish.Foundation.Transport.AddSunfishTransport()` — confirmed in `packages/foundation-transport/DependencyInjection/ServiceCollectionExtensions.cs:34`
- `Sunfish.Blocks.CrewComms.DependencyInjection.AddSunfishCrewComms()` — confirmed already wired (empty roster) in `accelerators/anchor/MauiProgram.cs:226`
- `IChannelProvider`, `IChannelSession`, `IChannelInvitation`, `ICrewRoster`, `CrewMember`, `CrewPresence`, `PresenceStatus`, `ChannelCapability` in `Sunfish.Foundation.Channels`
- `QrOnboardingService` in `accelerators/anchor/Services/QrOnboardingService.cs` (team-pairing entry point; consumes `IEd25519Signer.GenerateKeyPair`)
- Razor-component-in-block precedent: `packages/blocks-assets/AssetCatalogBlock.razor`, `packages/blocks-forms/FormBlock.razor`, etc.

---

## Halt conditions (read before each phase)

1. `IChannelProvider` ctor signature in `packages/blocks-crew-comms/NativeChannelProvider.cs` has changed since this hand-off was authored — re-read before writing call-site code in Phase 3.
2. `ITransportSelector` interface has changed since this hand-off — re-read `packages/foundation-transport/ITransportSelector.cs`.
3. `ICrewRoster` interface has gained methods beyond `AddInMemory` — verify the interface surface in `packages/foundation-channels/ICrewRoster.cs` before authoring `TeamMembershipCrewRoster` in Phase 2.
4. `QrOnboardingService` peer-record shape has changed — re-read `accelerators/anchor/Services/QrOnboardingService.cs` for the post-pairing peer/team membership accessor before Phase 2.
5. A parallel session has already opened a PR touching `accelerators/anchor/MauiProgram.cs` § crew-comms wiring or `packages/blocks-crew-comms/SunfishChat.razor` — `gh pr list --state open --search "crew-comms"` and `git log --all --oneline -10` before each phase.
6. P4.5 hand-off addendum (`crew-comms-p45-stage06-addendum.md`) has been started by another COB session — Phase 3 (`CrewCommsListenerHostedService`) and Phase 4 (`SunfishChat`) MUST NOT depend on TYPING/DELIVERED message types; only `0x05 TEXT` is in scope. If P4.5 has already shipped, that's fine — UI can optionally surface TYPING/DELIVERED but MVP demo does not require it.
7. `BlazorWebView` host configuration in `accelerators/anchor/MainPage.xaml` has changed — verify `RootComponent Selector="#app"` still maps to `Components/Routes.razor` before adding `/chat` route in Phase 5.

Any halt-condition tripped → STOP, write `cob-question-*.md` to `/Users/christopherwood/Projects/SunfishSoftware/coordination/inbox/`, halt the workstream.

---

## Phase 1 — Anchor `AddSunfishTransport()` wiring (~30min, 1 PR — standard review)

**Files:**
- `accelerators/anchor/MauiProgram.cs` (1 line + Bridge relay endpoint config)

**Scope:** add `services.AddSunfishTransport()` call before `AddSunfishCrewComms` so `NativeChannelProvider`'s `ITransportSelector` dependency resolves. For LAN-only demo we need only Tier 1 (mDNS) — Tier 3 (Bridge relay) defaults are fine but won't be exercised.

```csharp
// Place AFTER ISyncDaemonTransport registration (line ~187), BEFORE AddSunfishCrewComms (line ~226)
builder.Services.AddSunfishTransport();
```

**Acceptance gate (binary):**
- `dotnet build accelerators/anchor` clean (no DI resolution warnings).
- `dotnet test accelerators/anchor/tests --filter MauiProgram*` green (existing tests still pass).
- New unit test: `MauiProgram_RegistersTransportSelector_BeforeCrewComms` resolves `ITransportSelector` from the built service provider without throwing.

---

## Phase 2 — `TeamMembershipCrewRoster` adapter (~2h, 1 PR — standard review)

**Files:**
- `accelerators/anchor/Services/TeamMembershipCrewRoster.cs` (new) — adapts the team membership directory to `ICrewRoster`
- `accelerators/anchor/MauiProgram.cs` — replace empty `AddInMemory(Array.Empty<CrewMember>())` with `services.AddSingleton<ICrewRoster, TeamMembershipCrewRoster>()`
- `accelerators/anchor/tests/TeamMembershipCrewRosterTests.cs` (new) — at least 6 cases: empty team, single peer, multiple peers, peer-removed-after-team-leave, tenant-isolation, post-add-team-join refresh

**Scope:** wire the active team's pairing-derived peer list into `ICrewRoster` so the QR-pairing flow drives roster membership without manual seed code. Per CO direction 2026-05-06, demo uses the existing `QrOnboardingService` flow — when two Macs complete pairing into the same team, each Mac's `TeamMembershipCrewRoster` should surface the other as a `CrewMember`.

**Implementation:**
- Inject `IActiveTeamAccessor` (already in DI; resolves the user's currently-active team)
- Inject `ITeamMembershipReader` (or whatever the existing membership-query surface is — `ITeamContextFactory.GetOrCreateAsync` and the surrounding API in the QR onboarding flow surface this; **read `accelerators/anchor/Services/QrOnboardingService.cs` first to find the right symbol**)
- On `GetMembersAsync(TenantId, ct)`: enumerate peer entries for the active team, project each `(PeerId, DisplayName, ChannelCapability.Text)` to `CrewMember`
- Subscribe to team-membership-change events if available; otherwise short cache (60s) is acceptable for MVP

**HALT** if: `ITeamMembershipReader` (or equivalent) does not exist on origin/main — file `cob-question-*.md` to coordination inbox describing the substrate gap. DO NOT add a new substrate type; that's XO-territory.

**Acceptance gate (binary):**
- `dotnet build accelerators/anchor` clean
- 6 unit tests green (mock `IActiveTeamAccessor` + `ITeamMembershipReader`)
- After QR-pairing two Anchor instances into the same team, `ICrewRoster.GetMembersAsync` returns the partner's `(PeerId, DisplayName, Text)` on both sides — verified manually or via integration test.

---

## Phase 3 — `CrewCommsListenerHostedService` (~2h, 1 PR — standard review)

**Files:**
- `accelerators/anchor/Services/CrewCommsListenerHostedService.cs` (new)
- `accelerators/anchor/Services/CrewCommsInvitationBus.cs` (new) — pub/sub bridge between `IChannelProvider.ListenAsync` and the UI subscription
- `accelerators/anchor/MauiProgram.cs` — register hosted service + invitation bus
- `accelerators/anchor/tests/CrewCommsListenerHostedServiceTests.cs` (new) — at least 5 cases: starts on app boot, drains invitations, disposes cleanly on shutdown, handles `ListenAsync` exception (logs + retries with backoff), forwards through `CrewCommsInvitationBus`

**Scope:** drive `IChannelProvider.ListenAsync` continuously and route incoming `IChannelInvitation` instances to a UI-observable bus. Mirror the pattern in `accelerators/anchor/Services/AnchorSyncHostedService.cs` (long-running BackgroundService with cancellation handling).

**`CrewCommsInvitationBus` contract:**
```csharp
public interface ICrewCommsInvitationBus
{
    IObservable<IChannelInvitation> InboundInvitations { get; }
    IObservable<CrewPresence> PresenceUpdates { get; }
}
```

UI consumes via `@inject ICrewCommsInvitationBus`; bus implements `IDisposable` and tracks subscriptions per Blazor component lifecycle.

**Acceptance gate (binary):**
- `dotnet build accelerators/anchor` clean
- 5 unit tests green
- Manual: launching Anchor with a fresh team starts the listener; logs show `[CrewCommsListener] Listening on tier=mDNS` (or whichever tier `ITransportSelector` chose) within 2s of `MainPage` appearance.

---

## Phase 4 — `SunfishChat` Blazor component in `blocks-crew-comms` (~4h, 1 PR — pre-merge council mandatory: WCAG/a11y subagent + 4-perspective)

**Files:**
- `packages/blocks-crew-comms/SunfishChat.razor` (new) — main component
- `packages/blocks-crew-comms/SunfishChat.razor.css` (new) — scoped styles
- `packages/blocks-crew-comms/_Imports.razor` (new) — Blazor `@using` declarations
- `packages/blocks-crew-comms/Sunfish.Blocks.CrewComms.csproj` — add `Microsoft.AspNetCore.Components.Web` package reference + `<UseRazorSourceGenerator>true</UseRazorSourceGenerator>` if not already enabled per cohort precedent (read `packages/blocks-assets/Sunfish.Blocks.Assets.csproj` first)
- `packages/blocks-crew-comms/tests/SunfishChatTests.cs` (new) — bUnit tests; at least 8 cases: renders with empty roster, renders presence dots (online/offline/unknown), shows invite button per online peer, dispatches `OpenAsync` on invite-click, displays incoming invitation prompt, renders message thread in send-order, send button disabled when input empty, scrollback preserved on new message arrival.

**Component surface (parameters + events):**
```razor
<SunfishChat Provider="@ChannelProvider"
             Roster="@CrewRoster"
             InvitationBus="@InvitationBus"
             ActiveSession="@_session"
             OnSessionOpened="@HandleSessionOpened"
             OnSessionClosed="@HandleSessionClosed" />
```

**MVP UX (per CO 2026-05-06):**
- **Presence panel (left):** vertical list of `CrewMember` rows. Each row: presence dot (green=online, gray=offline, amber=stale-presence) + display name + tap-to-invite button (disabled when offline)
- **Thread panel (right):** scrollable message list (most-recent-bottom, like every chat app); each message rendered as `<div class="chat-msg" data-from="local|remote">{plaintext}</div>`; new messages auto-scroll-to-bottom unless user has scrolled up (preserve manual scroll position)
- **Send input (bottom):** single-line `<input type="text">` + send button; Enter to send; disabled when no active session
- **Invitation prompt (modal/overlay):** when `InvitationBus.InboundInvitations` fires, show `Accept` / `Reject` buttons with the inviter's display name + tenant binding (tenant displayed for transparency, not for user verification)
- **No TYPING / DELIVERED indicators** in v0 (keep MVP scope tight; P4.5 PR 2/3 add these later)

**Accessibility (non-negotiable per ADR 0065 §7):**
- Presence dots: `aria-label="Online" / "Offline" / "Last seen 30s ago"` and use semantic colors plus textual indicator (not color-alone) — WCAG SC 1.4.1
- Thread: `role="log" aria-live="polite" aria-atomic="false"` so screen readers announce new messages
- Invitation prompt: `role="alertdialog" aria-modal="true"` with focused Accept button
- Send input: `<label>` association; keyboard navigation full coverage

**Pre-merge council mandatory** — WCAG/a11y subagent canonical per ADR 0065 §7 + 4-perspective (Outside Observer + Pessimistic Risk + Skeptical Implementer + Devil's Advocate). Apply Critical + Major findings before merge.

**Acceptance gate (binary):**
- `dotnet build packages/blocks-crew-comms` clean
- 8 bUnit tests green
- axe-core via `tooling/a11y-audit-runner` — zero Critical, zero Serious findings
- WCAG/a11y subagent + 4-perspective council reviews complete; Critical + Major findings applied or explicitly amended

---

## Phase 5 — Anchor `CrewChatPage.razor` consumer page (~1h, 1 PR — standard review with WCAG check)

**Files:**
- `accelerators/anchor/Components/Pages/CrewChatPage.razor` (new)
- `accelerators/anchor/Components/Layout/NavMenu.razor` — add `<NavLink>` to `/chat` (after the existing `/teams` link)
- `accelerators/anchor/Components/Pages/CrewChatPage.razor.cs` (code-behind if needed)
- `accelerators/anchor/tests/CrewChatPageTests.cs` (new) — at least 3 cases: page resolves required services, falls through to no-team-selected state when `IActiveTeamAccessor.Current is null`, navigates to `/teams` when user clicks "join a team" empty-state button.

**Scope:** mirror the `TeamSwitcherPage.razor` pattern (`@page "/teams"` + sidebar layout + service injection). Page hosts `<SunfishChat>` and supplies the four parameters from DI.

**Page route:** `@page "/chat"`

**Empty-state UX:** when `IActiveTeamAccessor.Current is null`, render a "Join a team to start chatting" panel with a button linking to `/teams`. Otherwise render `<SunfishChat>`.

**Acceptance gate (binary):**
- `dotnet build accelerators/anchor` clean
- 3 page tests green
- Manual: Anchor app starts → navigate to `/chat` → presence list renders for the active team's members → tapping a peer's invite button shows the receiver's invitation prompt within 2s.

---

## Phase 6 — apps/docs + ledger flip (~1h, 1 PR — standard review)

**Files:**
- `apps/docs/blocks/crew-comms/anchor-mvp.md` (new) — operator guide for the demo: how to pair two Macs, what to expect, troubleshooting
- `apps/docs/blocks/crew-comms/toc.yml` — add `anchor-mvp.md` entry
- `apps/docs/blocks/crew-comms/overview.md` — add "Anchor MVP" section with link to `anchor-mvp.md` and a "Phase 1 demo path: LAN-only mDNS" callout
- `icm/_state/active-workstreams.md` — flip W#59 row → `built` with PR list
- `_shared/engineering/adapter-parity.md` — note React `SunfishChat` parity is **deferred** per CO 2026-05-06 directive; tracked as separate workstream W#XX (XO will file separately)

**Acceptance gate:** docs render via `apps/docs` build; ledger row reflects merged reality.

---

## Final demo acceptance criteria (binary, all 6 must pass)

1. Two Anchor instances launched on two separate Macs on the same Wi-Fi network
2. Each Mac completes the QR-pairing flow into the same team via `QrOnboardingService` — existing flow, unchanged by this workstream
3. Within 5s of post-pairing app return-to-foreground, each Mac's `/chat` presence list shows the partner's display name with a green online dot
4. Mac A taps Mac B's invite button → Mac B sees an invitation prompt within 2s with Mac A's display name
5. After Mac B accepts, both Macs send/receive plain-text messages with scrollback preserved across at least 20 messages each direction
6. Closing Mac A's app moves Mac A's presence dot on Mac B to offline within 45s (ADR 0076 presence TTL)

**Demo dry-run script** (~10 minutes):
1. `dotnet build && dotnet publish -f net10.0-maccatalyst` × 2 Macs
2. Launch Anchor on Mac A, complete onboarding, generate team-invite QR
3. Launch Anchor on Mac B, scan QR via `QrScanner.razor`, complete pairing
4. On both Macs, navigate to `/chat`
5. Verify presence + invite + send flow per acceptance criteria 3–6
6. Capture screen recording for post-demo retrospective

---

## Council-review canonical checks (Phase 4 + 5)

Before merging any UI-bearing phase:
- WCAG 2.2 AA + EN 301 549 v3.2.1 baseline (axe-core via `tooling/a11y-audit-runner`)
- 4-perspective council (Outside Observer / Pessimistic Risk / Skeptical Implementer / Devil's Advocate) — Opus 4.7 xhigh
- Cohort batting average for UI-bearing phases (per `feedback_council_reviews_use_best_model_xhigh`): expect 1–3 Critical + 4–8 Major findings; if zero findings, council was insufficient — re-run with broader scope.

---

## What unblocks this workstream

Nothing — all hard prerequisites are on origin/main as of 2026-05-06. COB may begin Phase 1 immediately on receipt of this hand-off.

## What this workstream blocks

- **Demo to CO + stakeholders** (target: end of week 2026-05-08)
- **W#XX (TBD) — `SunfishChat` React adapter parity** — XO files separately once MVP demo lands; explicitly out of scope per CO 2026-05-06
- **P4.5 hand-off addendum (TYPING + DELIVERED + transcript-hash-A1+A2 + glare-wiring)** is independent of this workstream; can ship in parallel or post-demo

## Memory pointers

- `feedback_council_reviews_use_best_model_xhigh` (council canonical model + effort)
- `feedback_audit_existing_blocks_before_handoff` (read packages/blocks-* precedent first — done)
- `project_workstream_45_crew_comms` (W#45 substrate context)
- ADR 0065 §7 — WCAG/a11y subagent mandate non-negotiable for UI-bearing phases
- ADR 0076 §A2 — capability negotiation; demo only exercises `ChannelCapability.Text`
