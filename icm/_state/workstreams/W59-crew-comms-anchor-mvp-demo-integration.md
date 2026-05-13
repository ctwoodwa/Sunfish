---
sort_order: 67
number: 59
slug: crew-comms-anchor-mvp-demo-integration
title: "**Crew Comms Anchor MVP Demo Integration** (`sunfish-feature-change` pipeline) — LAN-only mDNS demo path: `AddSunfishTransport()` wiring + `TeamMembershipCrewRoster` adapter + `CrewCommsListenerHostedService` + `SunfishChat` Blazor component in `blocks-crew-comms/` + Anchor `/chat` consumer page"
status: "built"
status_cell: "`built` — all 6 phases merged: P1 AddSunfishTransport #720, P2 TeamMembershipCrewRoster #721, P3 CrewCommsListenerHostedService #722, P4 SunfishChat.razor #723, P5+6 CrewChatPage+docs #724; P4.5 glare addendum #726 also merged"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/crew-comms-anchor-mvp-stage06-handoff.md` + `docs/adrs/0076-crew-comms-foundation-channels.md` (Accepted; A1+A2+A3 landed) + W#45 substrate (`packages/foundation-channels/` + `packages/blocks-crew-comms/` on origin/main) + W#30 transport (`packages/foundation-transport/` on origin/main with `AddSunfishTransport()`) + `accelerators/anchor/Services/QrOnboardingService.cs` (existing pairing flow, unchanged)"
---

## Notes

**Hand-off authored 2026-05-06.** Closes the four MVP-demo gaps over W#45 substrate: (A) Anchor doesn't register transport, (B) empty `ICrewRoster`, (C) no `ListenAsync` driver, (D) no UI. **Demo target:** Mac↔Mac on same Wi-Fi via mDNS only (Bridge relay path explicitly out of scope). 6 phases / ~5 PRs / 10–13h: P1 `AddSunfishTransport()` (~30m), P2 `TeamMembershipCrewRoster` adapter (~2h), P3 `CrewCommsListenerHostedService` + `CrewCommsInvitationBus` (~2h), P4 `SunfishChat.razor` in `blocks-crew-comms/` (~4h, pre-merge council mandatory: WCAG/a11y subagent + 4-perspective canonical), P5 Anchor `CrewChatPage.razor` consumer (~1h), P6 apps/docs + ledger flip (~1h). **MVP UX:** presence dots + invite buttons + plaintext thread + scrollback. **Explicitly NOT in scope:** TYPING/DELIVERED/transcript-hash-A1+A2/glare (P4.5 hand-off addendum — independent, can ship in parallel or post-demo); audio (Phase 3); video (Phase 4); React `SunfishChat` parity (deferred per CO 2026-05-06 directive, separate workstream W#XX TBD once MVP lands; adapter-parity matrix will note explicit deferral with date + driver). 7 halt-conditions named including substrate-signature drift on `IChannelProvider`/`ITransportSelector`/`ICrewRoster`/`QrOnboardingService` peer-record shape, parallel-PR detection, P4.5 ordering check, and `BlazorWebView` host config drift. Pre-merge council canonical (Phase 4 + 5 UI-bearing). **Unblocks:** end-of-week demo to CO + stakeholders (target 2026-05-08).
