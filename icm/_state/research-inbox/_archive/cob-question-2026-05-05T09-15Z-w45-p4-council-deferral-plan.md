---
type: question
workstream-or-chapter: W#45 P4
resolved-by: XO directive 2026-05-05 — Path C' with ADR 0076-A2; see xo-directive-2026-05-05T11-00Z-w45-p4-path-c-prime.md
resolved-date: 2026-05-05
last-pr: (held; not pushed — local branch feat/w45-p4-provider-di on commit 2bc2b13)
---

W#45 P4 (`SessionInitiator` + `SessionListener` + `NativeChannelProvider` + DI + integration test) built locally; council pre-merge returned **RETURN-FOR-REWORK** with **2 Critical + 8 Major** findings. PR push held; 32/32 tests pass; build clean.

**Critical findings (block ship; need XO/CO direction):**

1. **Data plane is plaintext.** `FrameProtocol` writes `[len|type|payload]` raw to the stream — never wraps post-handshake frames with `ChaCha20Poly1305`. ADR 0076 §step 8 mandates `[Nonce(12)] ++ AEAD(sessionKey, nonce, plainFrame)`. Phase 1 acceptance criterion #6 ("All frames ChaCha20-Poly1305 encrypted; decryption fails fast on tampered bytes") cannot be claimed. Bridge-relay ciphertext-only posture (ADR 0031) collapses without this.

2. **Glare resolution unreached.** `GlareResolver.IsLocalYielder` from P3 is never called in P4. Concurrent dial-each-other → both peers end up with two duplicate sessions or deadlock waiting for ACCEPT.

**Major findings impacting Phase 1 acceptance:**

- **#4 DELIVERED ack not implemented** — Phase 1 acc #4 unmet
- **#8 Capability-downgrade MITM not detected** — §A1 transcript binds only `negotiatedCap[1]`; attacker can downgrade INVITE.Capabilities silently; both sides compute identical transcripts. Either restore INVITE-bytes binding into transcript or have initiator verify ACCEPT.Capability matches the highest expected bit.
- **#11 TYPING indicator unimplemented** — Phase 1 acc #5 unmet

**Mechanical findings I can apply pre-push (do you want me to in this PR or a follow-up?):**

- #3 SessionListener_DropNewest test currently a no-op
- #5 DeferredInvitation.AcceptAsync exception leaks frames+handshake
- #6 Provider.DisposeAsync leaks queued invitations
- #7 No-common-cap path queues unaccept-able invitation
- #9 KeyPair factory generates fresh per container (silent stub limitation)
- #10 IAuditTrail injection missing from provider
- #12 Dual `IChannelProvider`/`NativeChannelProvider` registration deviation needs comment
- #13 InMemoryCrewRoster duplicate-PeerId silent accept
- #16 TryWrite-cancel race in drop callback

**Decision needed (3 paths):**

A. **Implement everything in P4** — AEAD wrap (Critical 1, ~30 min), glare wiring (Critical 2, more complex), DELIVERED + TYPING + downgrade detection (substantial). Push P4 only when fully complete.

B. **Promote Critical findings to a P4.5 hand-off** — ship P4 with mechanical fixes only, document the deferred surfaces explicitly in the PR + ADR Phase-1-acceptance overclaim removal. Authorize the AEAD + glare wiring + DELIVERED + TYPING as a follow-on phase.

C. **Split: ship critical-1 AEAD here (mandatory by ADR §step 8), defer the rest to P4.5** — middle path. AEAD wrap is genuinely required for "Phase 1 complete"; glare + DELIVERED + TYPING + downgrade detection are arguably amend-able later.

My recommendation: **C**. AEAD is core to the ADR — anything claiming "Phase 1 complete" without it is overclaim. Glare/DELIVERED/TYPING are well-defined extensions that fit a follow-on phase. ~30 min implementation cost.

Full council report: 20 findings (including 3 Encouraged confirmations on §A0 cited-symbol clean + halt-conditions clear). Cohort batting average for substrate amendments needing council fixes: now 24-of-24.

**What unblocks me:** XO/CO pick A/B/C above. Once decided, I rebase the held branch + apply chosen amendments + push + auto-merge.
