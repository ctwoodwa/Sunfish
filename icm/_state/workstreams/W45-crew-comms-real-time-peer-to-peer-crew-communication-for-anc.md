---
sort_order: 48
number: 45
slug: crew-comms-real-time-peer-to-peer-crew-communication-for-anc
title: "**Crew Comms** — real-time peer-to-peer crew communication for Anchor (`sunfish-feature-change` pipeline)"
status: "built"
status_cell: "`built` (Phase 1 substrate complete 2026-05-05; P4.5 follow-up tracked)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/foundation-channels-crew-comms-stage06-handoff.md` + `docs/adrs/0076-crew-comms-foundation-channels.md` (+ A1 PR #564 + A2 PR #566 + A3 conformance vectors)"
---

## Notes

**Phase 1 substrate built 2026-05-05.** PRs: P1 #546 (`foundation-channels` contracts), P2 #557 (`blocks-crew-comms` Protocol/+Crypto/+`KeyPair.Sign`), P3 #560 (`Session/`+`Presence/`+`Signaling/GlareResolver`), P4 #568 (`SessionInitiator`+`SessionListener`+`NativeChannelProvider`+DI+AEAD wrap+integration test), P5 (this PR; Anchor wiring + apps/docs/blocks/crew-comms + WebSocketDuplexStream threading note). Security: Ed25519-signed HELLO + HEARTBEAT; CONFIRM transcript-hash frame; TenantId binding; NSec.Cryptography (X25519 + HKDF + ChaCha20-Poly1305); ChaCha20-Poly1305 AEAD wrap on every post-HELLO frame with role-split nonce (initiator bit-63=0, responder=1); no session resume; capability-subset verification per A2; bounded ListenAsync `Channel(16)` with `Wait` mode + sync `TryWrite` drop-detection. **P4.5 hand-off authored 2026-05-05** at `icm/_state/handoffs/crew-comms-p45-stage06-addendum.md` — 3 PRs, ~4-6h: PR 1 transcript-hash alignment (security; pre-merge council), PR 2 TYPING+DELIVERED (standard review), PR 3 glare-wiring (pre-merge council). **A3 conformance test vectors authored 2026-05-05** (this PR `docs/adr-0076-a3-test-vectors`) — closes A1 council finding F3; 9 vectors (3 HELLO + 3 HEARTBEAT + 3 CONFIRM); generator at `tools/icm/generate-channel-vectors.py`; canonical artifact at `tools/icm/channel-test-vectors.json`; A3 vectors will be consumed as known-answer fixtures by P4.5 PR 1 (transcript-hash alignment). **Cohort batting average for substrate amendments needing council fixes: 24-of-24** (P2 council → 1 Critical + 4 Major; P4 council → 2 Critical + 8 Major).
