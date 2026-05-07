# Crew Comms — Anchor MVP walkthrough (W#59)

This page describes the LAN-mDNS Mac↔Mac demo path delivered by W#59
across phases 1–6. The goal: two macOS Anchor instances on the same
Wi-Fi network can pair into a team, see each other in their chat
roster, exchange invitations, and send plain-text messages.

## What W#59 ships

| Phase | Deliverable | PR |
|-------|-------------|----|
| 1 | `AddSunfishTransport()` + `AddBridgeRelay(...)` wiring in `MauiProgram.cs` | #720 |
| 2 | `TeamMembershipCrewRoster` adapter (mDNS-discovered peers → `ICrewRoster`) | #721 |
| 3 | `CrewCommsListenerHostedService` + `CrewCommsInvitationBus` | #722 |
| 4 | `SunfishChat.razor` Blazor component in `blocks-crew-comms` | #723 |
| 5 | Anchor `CrewChatPage` consumer page (`@page "/chat"`) + NavMenu link | (this PR) |
| 6 | apps/docs walkthrough + ledger flip | (this PR) |

## Demo flow (Mac↔Mac, same Wi-Fi)

1. Build + launch Anchor on Mac A (`dotnet build accelerators/anchor; dotnet run`).
2. Repeat on Mac B.
3. On Mac A, complete the QR-onboarding flow to create a team.
4. On Mac B, scan Mac A's join-team QR (Wave 6.8 multi-team flow).
5. Both Macs see "Chat" in the nav menu post-onboarding.
6. Click **Chat** on Mac A. The presence panel shows Mac B (the
   `TeamMembershipCrewRoster` adapter projects every same-`TeamId`
   peer the `IPeerDiscovery` substrate has discovered).
7. Click **Invite** next to Mac B. Mac A's `IChannelProvider.OpenAsync`
   triggers the W#45 substrate handshake (HELLO → INVITE → ACCEPT →
   CONFIRM transcript exchange).
8. On Mac B, the `CrewCommsListenerHostedService` pulls the invitation
   from `IChannelProvider.ListenAsync` and pushes it onto
   `ICrewCommsInvitationBus.InboundInvitations`. The `SunfishChat`
   component sees the invitation and renders a `role="alertdialog"`
   prompt; focus lands on the **Accept** button automatically (WCAG
   2.4.3 + alertdialog convention).
9. Mac B clicks **Accept** → `IChannelInvitation.AcceptAsync` resolves
   the active session → `OnSessionOpened` callback fires →
   `CrewChatPage` stashes the session → both panes show enabled
   send-input + Send button.
10. Type a message on either side and hit **Send** → `SendTextAsync`
    queues the frame on the AEAD-wrapped session →
    `_messages.Add(...)` appends to the rendered `<ol role="log"
    aria-live="polite">` thread.

## What's deliberately out of scope

W#59 is the **demo path**, not the production feature. These are
follow-up workstreams:

- **W#45 P4.5 PR 2** — TYPING + DELIVERED indicators.
- **W#45 P4.5 PR 3** — Glare-wiring (deterministic dedup when both
  peers `OpenAsync` to each other concurrently).
- **W#45 P4.5 PR 1** — Transcript-hash A1+A2 alignment (closes the
  relay-MitM capability-downgrade vector). Already merged as #718;
  not exercised on the LAN-mDNS path because Tier 3 isn't reached.
- **Bridge relay path** — out of scope for the LAN demo; the Tier 3
  registration is a placeholder URL.
- **Audio (Phase 3 ADR 0076)** — Opus push-to-talk on a future wave.
- **Video (Phase 4 ADR 0076)** — same.
- **React `SunfishChat` parity** — deferred per CO 2026-05-06; will
  be filed as a separate workstream once the MAUI demo is signed off.
- **Inbound message receive-pump** — `SunfishChat` v0 only renders
  outbound messages; an `ActiveSession.ReceiveTextAsync` consumer is
  a follow-up amendment.
- **Live presence updates** — `TeamMembershipCrewRoster` returns
  static "Online" for any roster entry; the `PresenceBus` wiring
  surfaces stale/offline state in a follow-up.

## Halt-condition recovery

If the demo doesn't work end-to-end, walk these:

1. **Both Macs see each other in mDNS** — `dns-sd -B _sunfish._tcp.`
   should list both nodes' advertisements. If empty, check Wi-Fi
   connectivity + firewall (mDNS uses UDP/5353).
2. **Both Macs share a team** — `accelerators/anchor` logs should
   show "active team: \<TeamId\>" matching on both sides.
3. **Listener is running** — Anchor logs include
   `[CrewCommsListener] Listening on tenant=\<TenantId\>` after
   active-team materialization.
4. **Substrate handshake succeeds** — the W#45 cohort's
   `NativeChannelProviderIntegrationTests.EndToEnd_TextExchange_BetweenTwoProviders`
   on origin/main is the canonical reference. If the in-process
   integration test fails, the demo fails too.
5. **CrewChatPage resolves all 5 services** — the
   `CrewChatPage_HasRequiredInjectableProperties` test is the
   compile-time pin; if it fails on origin/main, MauiProgram is
   missing one of the W#59 P1/P2/P3 wirings.

## Architecture summary

```
QR-pairing flow (existing)              W#59
─────────────────────────       ───────────────────────
   Mac A ←→ Mac B                  Mac A             Mac B
   (founder + joiner               ──────            ──────
    share a TeamId)
                                   /chat (P5)       /chat (P5)
                                     │                │
                                     ▼                ▼
                                SunfishChat (P4)   SunfishChat (P4)
                                  ▲    ▲             ▲    ▲
                                  │    │             │    │
                          IChannelProvider       IChannelProvider
                                  │                  │
                          NativeChannelProvider   NativeChannelProvider
                                  │                  │
                          ITransportSelector     ITransportSelector
                                  │                  │
                              MdnsPeer ←──Wi-Fi──→ MdnsPeer
                                       (W#30)
```

Roster (P2) drives presence panel; Listener (P3) drains incoming
invitations onto the bus; Component (P4) renders both. All four
pieces register in `MauiProgram.cs` (P1) before `AddSunfishCrewComms`.
