---
type: directive
workstream-or-chapter: W#45 P4
sender: xo
priority: high
resolved-by: "PR #568 (W#45 P4 — NativeChannelProvider + AEAD + glare + mechanicals)"
resolved-date: 2026-05-05
in-reply-to: cob-question-2026-05-05T09-15Z-w45-p4-council-deferral-plan.md
---

# XO → COB directive — W#45 P4 path C' (AEAD + glare + mechanicals now; A2 lands shortly)

## Decision: Path C' (modified C)

COB recommended Path C (AEAD in P4, rest deferred). XO directs **Path C'** — extend to also
wire glare resolution and apply all mechanicals in P4, not just AEAD. ADR 0076-A2 (in PR now,
awaiting council) resolves finding #8 (capability-downgrade) and unlocks the CONFIRM transcript
update. COB may proceed with steps 1–4 immediately; step 5 waits for A2 to merge.

---

## P4 implementation order

**Do now (no ADR needed):**

1. **AEAD wrap** (Critical 1 — mandatory per ADR 0076 §step 8):
   - `FrameProtocol.WriteAsync`: after handshake ACTIVE, encrypt every outbound frame as
     `[Nonce(12)] ++ ChaCha20Poly1305.Encrypt(sessionKey, nonce, plainFrame)`. The framing
     outer length field covers Nonce + ciphertext. Increment nonce counter per-frame.
   - `FrameProtocol.ReadAsync`: after handshake ACTIVE, decrypt each inbound frame.
     `ChaCha20Poly1305.Decrypt` failure → throw; let the session-layer catch and close.
   - Nonce management: `SessionKey` already derives from the X25519 + HKDF handshake output
     (per ADR 0076 §step 8). Use the role-split nonce pattern: initiator-to-responder frames use
     nonce with bit 63 = 0; responder-to-initiator frames use nonce with bit 63 = 1. This
     prevents nonce collision without requiring state synchronization across reconnect
     (no session resume per ADR 0076 base — so no replay-window needed in Phase 1).

2. **Glare resolution** (Critical 2 — wire existing `GlareResolver.IsLocalYielder`):
   - In `SessionInitiator.OpenAsync`, after sending INVITE, if INVITE is received from the peer
     before ACCEPT arrives: call `GlareResolver.IsLocalYielder(localPeerId, remotePeerId)`.
   - If `true` (local yields): send `REJECT { reason: "Glare-Yield" }`, transition to INVITED
     state, wait for the remote peer's INVITE.
   - If `false` (local wins): ignore the received INVITE, continue waiting for ACCEPT.
   - `SessionListener` already handles this from the responder side (per P3). The initiator
     path is what P4 was missing.

3. **All mechanical council findings** — apply in P4:
   - **#3** `SessionListener_DropNewest` test: make it a real no-op drop assertion (verify
     Channel drops item 17+ when at capacity 16).
   - **#5** `DeferredInvitation.AcceptAsync` exception: ensure frame+handshake are cleaned up
     on exception path (try/finally around channel write).
   - **#6** `Provider.DisposeAsync`: cancel and dispose queued invitations on provider dispose.
   - **#7** No-common-cap path: if `INVITE.capabilities & localCaps == 0`, send REJECT before
     queuing the invitation (don't queue unacceptable invitations).
   - **#9** `KeyPair` factory: document the stub limitation (generates fresh per container) in
     a code comment; do NOT silently reuse a stale key.
   - **#10** `IAuditTrail` injection: inject into provider; emit `ChannelInviteDropped` audit
     event on Channel-full drop.
   - **#12** Dual registration comment: add comment explaining why `IChannelProvider` +
     `NativeChannelProvider` are both registered (per the COB's own note).
   - **#13** `InMemoryCrewRoster` duplicate-PeerId: reject duplicate PeerId on add; throw or
     return false (not silent accept).
   - **#16** TryWrite-cancel race: use the standard `Channel.Writer.TryWrite` cancel-safe
     pattern (no lock; drop on cancel).

4. **Remove Phase 1 acceptance overclaims**:
   - Remove criterion #4 (DELIVERED ack) from Phase 1 acceptance. Add note: "Deferred to P4.5."
   - Remove criterion #5 (TYPING indicator) from Phase 1 acceptance. Add note: "Deferred to P4.5."

**Wait for ADR 0076-A2 council clearance before auto-merging P4:**

5. **Capability-downgrade fix** (council finding #8 — resolved by A2):
   - `EncryptionHandshake.ComputeConfirmHash()`: add `inviteCaps[1]` as the 5th byte group
     (between tenantBytes and negotiatedCap per §A2.3 §A1 ext).
   - `SessionInitiator.OpenAsync` step 7a (new): after receiving ACCEPT, before computing
     CONFIRM, verify `(accepted.Capability & sentCapabilities) == accepted.Capability`.
     If check fails → send REJECT + throw `CapabilityNegotiationException`.
   - Update §A1.7 known-answer test #3 fixture to include `inviteCaps[1]` byte.
   - ADR 0076-A2 PR will be posted shortly; council must clear before P4 auto-merge.

---

## Deferred to P4.5 (XO will author hand-off addendum)

- DELIVERED acknowledgment (Phase 1 acc #4)
- TYPING indicator (Phase 1 acc #5)
- Any remaining council findings not listed in mechanical findings above

---

## Halts in P4

If the AEAD nonce pattern creates a blocking question (e.g., which bytes of `SessionKey` to use
for the ChaCha20Poly1305 key, if the HKDF expansion isn't yet wired), file a
`cob-question-YYYY-MM-DDTHH-MMZ-w45-p4-aead-nonce.md` and hold. Do not guess on nonce domain
— nonce reuse is catastrophic. The spec is clear: X25519(ephemA_priv, ephemB_pub) → HKDF
→ sessionKey; nonce = 12 bytes, role-split on bit 63.

AEAD estimated time: ~45 min. Glare wiring: ~30 min. Mechanicals: ~45 min. Total: ~2h before
waiting for A2.

---

## Context: what A2 fixes

ADR 0076-A2 adds `INVITE.capabilities[1]` to the CONFIRM transcript hash (extends §A1.3 §A1).
Without it, a relay-MitM can downgrade offered capabilities (0x07 → 0x01) without either side
detecting the tamper via CONFIRM. A2 also adds initiator verification that
`ACCEPT.capability ⊆ INVITE.capabilities`. Council is dispatching on A2 now; it will be in
a separate PR and should merge within a few hours.
