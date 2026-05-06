---
id: "0076"
title: Crew Comms — foundation-channels Contracts and Native Implementation
status: Accepted
date: 2026-05-04
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - distribution
  - security
  - identity

enables:
  - crew-comms-text
  - crew-comms-audio
  - crew-comms-video

composes:
  - 61   # three-tier peer transport
  - 28   # CRDT + local-first sync
  - 31   # Bridge hybrid multi-tenant
  - 46   # key management + recovery

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments:
  - date: 2026-05-04
    summary: >
      Council review amendments applied: Ed25519 HELLO + HEARTBEAT signing; CONFIRM transcript-hash
      frame (0x0A); tenant binding in HELLO; no-session-resume mandate; speculative relay HELLO
      bootstrap (P4 chicken-and-egg fix); TTL 90s→45s + 20s in-session keepalive; glare resolution
      via PeerId comparison; ListenAsync bounded Channel(16) + drop audit; OpenAsync/OfferedCapabilities
      signature changed to ChannelCapability flags; Terminated→Completed Task<ChannelTerminationReason>;
      ReceiveTextAsync single-consumer contract; CloseAsync/DisposeAsync semantics; RFC 4122 UUID
      encoding mandate; IDuplexStream threading contract documented; stale P-256 text removed;
      NSec.Cryptography confirmed as sole new dependency; confidence HIGH.
  - date: 2026-05-05
    summary: >
      A1 wire-encoding ratification: CONFIRM transcript-hash uses reduced canonical form
      (ephemA||idA||ephemB||idB||uint32BE(len)||tenantBytes||negotiatedCap) instead of full frame
      bytes; TenantId encoding changed from uuid-16 to uint32BE-length-prefixed UTF-8 bytes in HELLO
      and HEARTBEAT signables and payloads; PeerId in HEARTBEAT changed from uuid-16 to raw 32-byte
      Ed25519 identity public key. Unblocks W#45 P2 COB RETURN-FOR-REWORK.
  - date: 2026-05-05
    summary: >
      A2 capability-negotiation verification: adds INVITE.capabilities[1] to CONFIRM transcript hash
      (extends A1 reduced form; detects relay-MitM INVITE capability-downgrade attack); adds
      initiator post-ACCEPT verification that ACCEPT.capability is a subset of INVITE.capabilities.
      Corrects A1 rationale error ("INVITE.capabilities subsumed by negotiatedCap" was wrong).
      Unblocks W#45 P4 council finding #8.
  - date: 2026-05-05
    summary: >
      A3 conformance test vectors: ships 9 deterministic test vectors (HELLO ×3, HEARTBEAT ×3,
      CONFIRM-transcript-hash ×3) that fix the A1+A2 ratified canonical encoding to authoritative
      byte sequences. Closes A1 council finding F3 (interop-falsifiability gap). Vectors generated
      by tools/icm/generate-channel-vectors.py (re-runnable; byte-stable); committed JSON at
      tools/icm/channel-test-vectors.json. Per-implementation conformance protocol specified
      in §A3.5. Domain-separator (F2) remains deferred to a future amendment.
---

# ADR 0076 — Crew Comms: `foundation-channels` Contracts and Native Implementation

**Status:** Accepted
**Date:** 2026-05-04
**Resolves:** W#45 Stage 01 Discovery — `icm/01_discovery/output/2026-05-04_crew-comms-discovery.md`

---

## Context

Anchor installations are isolated: each node stores tenant data locally and syncs asynchronously via the federation layer. But crew members — users on the same tenant — have no way to communicate with each other in real time. This is the last missing primitive for Anchor as a standalone productivity platform.

Crew Comms is an **alternative MVP path** alongside the property-management block suite. Two Anchor nodes exchanging text messages — then escalating to audio — is a self-contained, compelling demonstration that does not require the full domain stack. It also proves the cross-Anchor transport infrastructure (ADR 0061) under real conditions, which is required for every future peer-connectivity feature.

The design follows the Inverted Stack pattern: define contracts in `foundation-channels`, provide a native reference implementation in `blocks-crew-comms` (default-installed in Anchor), and leave the provider interface clean enough for future compat adapters (`compat-zoom`, `compat-teams`) to replace the native impl without touching the contracts layer. Compat adapters are explicitly out of scope for this ADR; the interface is sized for the native impl's needs only.

The communication scope is **intra-tenant, crew-to-crew only**. Both endpoints must be authenticated members of the same tenant. Inter-tenant federation and external communication are out of scope.

---

## Decision drivers

1. **Alternative MVP path** — delivers compelling Anchor functionality without depending on the full property-management block suite (paper §20.7 Zone A accelerator goals).
2. **Transport proof** — exercises `foundation-transport` (ADR 0061) across all three tiers under real workload conditions; LAN first, relay second.
3. **Provider-pattern discipline** — per the Inverted Stack principle, Sunfish owns the contracts; third-party providers (Zoom, Teams) can replace the native impl. The interface must not leak transport-layer details upward.
4. **Security-first by construction** — all channel traffic encrypted end-to-end; the Bridge relay (ADR 0031) must see only ciphertext. Ephemeral DH public keys authenticated with Ed25519 long-term identity keys to prevent MitM. One targeted library dependency (`NSec.Cryptography`) added to resolve Windows CNG's lack of Curve25519 support.
5. **Local-first discipline** — messages are AP record class (available/partition-tolerant); persisted locally on both nodes; session can resume after a transport interruption without message loss.
6. **Escalating capability phases** — text (Phase 1) proves the signaling + encryption stack; audio (Phase 3) adds codec + jitter buffer on top of the same protocol; video (Phase 4) is a follow-on workstream. Each phase adds to `ChannelCapability` flags without breaking prior sessions.
7. **Industry lessons applied** — typing indicators, message IDs + delivery receipts, capability downgrade negotiation, push-to-talk default for Phase 3 audio are incorporated from Zoom/Teams/Slack operational experience.

---

## Considered options

### Option A — Single `blocks-crew-comms` package (no separate foundation layer)

Put contracts and implementation in one block. Simpler dependency graph for v1.

- Pro: one package to ship, one ADR, faster.
- Con: compat adapters must take a `blocks-*` dependency to implement the interface — violates tier discipline. If contracts live in a block, `foundation-transport` cannot reference them without creating a circular dep. Provider pattern cannot be enforced at compile time.
- **Rejected.**

### Option B — `foundation-channels` (contracts) + `blocks-crew-comms` (impl) [ADOPTED]

Thin contract layer in foundation-tier; native implementation in block-tier. Mirrors the `foundation-integrations` / `blocks-messaging` split from ADR 0052.

- Pro: tier discipline enforced; compat adapters implement `foundation-channels`; `IChannelProvider` is DI-registrable without pulling in implementation details; consistent with Sunfish architecture pattern.
- Con: two packages to scaffold; slightly more ceremony.
- **Adopted.**

### Option C — Reuse `blocks-messaging` for real-time chat

Extend the existing durable-thread substrate (ADR 0052 Phase 2.1) with a "real-time" mode.

- Pro: one fewer package.
- Con: `blocks-messaging` is async/durable (email, SMS threads), backed by `foundation-integrations`. Real-time peer-to-peer via `foundation-transport` is a fundamentally different concern. Mixing them would corrupt the clean boundary ADR 0052 defines. The two packages serve different record classes (AP vs eventual-durable). 
- **Rejected.** Confirmed no collision in Stage 01 Discovery.

---

### Presence model options

**Push heartbeat (adopted):** each node broadcasts a heartbeat (30s period) to all known crew peers. TTL-eviction at **45s** (1.5× heartbeat period — tighter than 90s to ensure stale presence doesn't linger after a clean disconnect). mDNS TXT augmentation provides a LAN fast-path (instantaneous presence on the same network).

**In-session keepalive:** within an ACTIVE session, if no framed traffic (TEXT, AUDIO_FRAME, etc.) has been sent for 20s, the sender emits a HEARTBEAT to keep the connection alive and maintain the remote peer's TTL clock.

**Pull on demand (rejected):** probe latency before the UI renders; no "Sarah is typing" style signals possible; stale roster between polls.

**Always-open connection per peer (rejected):** too expensive for relay tier; opens a relay connection per crew member at startup.

**Relay-tier presence bootstrap (Phase 2):** the lazy-presence model creates a discovery problem over relay: `PresenceStatus.Available` is only set for peers with a recent HELLO, but you can't send a HELLO to a peer you can't see. Resolution: `PresenceBus` performs **speculative relay HELLO attempts** for peers in `ICrewRoster` not currently seen via mDNS. On startup and every 30s thereafter, for each roster peer with no active mDNS presence, `PresenceBus` requests a relay connection via `ITransportSelector` and sends HELLO. If the peer is online and responds, it enters the roster as `Available`; if no response within 10s, the connection is dropped and the peer remains `Offline`. This probe happens at most once per heartbeat period per unseen peer. Implementation constraint: probe attempts MUST be bounded by roster size; degenerate case (N=100 peers × relay round-trip) acceptable because Anchor Phase 1 crews are small (≤20 members).

---

### Encryption options

**Option A — TLS/SslStream (rejected):** requires certificate infrastructure (CA, cert issuance to Anchor nodes); awkward for peer-to-peer (designed for client-server); heavyweight.

**Option B — Noise Protocol Noise\_XX (rejected):** purpose-built and excellent, but no maintained .NET implementation; would require vendoring or writing a Noise library — introduces unreviewed cryptographic code.

**Option C — Ephemeral X25519 DH + HKDF-SHA256 + ChaCha20-Poly1305 via `NSec.Cryptography` (adopted):** X25519 on .NET BCL alone is platform-inconsistent — Windows CNG does not expose Curve25519, so BCL `ECDiffieHellman` with X25519 fails on Windows MAUI targets. `NSec.Cryptography` (MIT, ~200 KB, wraps libsodium) provides first-class X25519 + ChaCha20-Poly1305 + HKDF in a single well-audited package. One targeted library dependency is the correct resolution; a P-256 workaround would change the stated security properties. Zero other new dependencies; full forward secrecy per session; relay sees ciphertext only per ADR 0031.

---

## Decision

**Adopt Option B** (two-package split) with the protocol and contract surface below.

### Package layout

```
packages/foundation-channels/   — Sunfish.Foundation.Channels (contracts only)
packages/blocks-crew-comms/     — Sunfish.Blocks.CrewComms    (native reference impl)
```

`foundation-channels` dependencies: `foundation`, `foundation-transport`, `foundation-multitenancy`
`blocks-crew-comms` dependencies: `foundation-channels`, `foundation-transport`, `foundation-multitenancy`

### Wire protocol

**Framing** — length-prefix over `IDuplexStream`:

```
┌──────────────────┬────────────┬──────────────────────────┐
│  Length (4B LE)  │  Type (1B) │  Payload (Length−1 bytes) │
└──────────────────┴────────────┴──────────────────────────┘
```

`Length` covers `Type` + `Payload`. Max: 64 KB control frames; 256 KB media frames (Phase 3+).
Payload encoding: MessagePack (binary-native; avoids base64 overhead on audio frames).

**Message type registry (v1):**

| Byte | Name | Direction | Payload |
|---|---|---|---|
| `0x01` | `HELLO` | bidirectional on connect | `{ ephemeralPublicKey: bytes[32], identityPublicKey: bytes[32], tenantId: uuid, signature: bytes[64], presence: PresenceHeartbeat }` — `signature` = Ed25519(longTermPrivKey, ephemeralPublicKey \|\| identityPublicKey \|\| tenantId) |
| `0x02` | `HEARTBEAT` | broadcast | `{ peerId: uuid, tenantId: uuid, caps: uint8, timestamp: int64, signature: bytes[64] }` — `signature` = Ed25519(longTermPrivKey, peerId \|\| tenantId \|\| caps \|\| timestamp) |
| `0x03` | `INVITE` | initiator → recipient | `{ capabilities: uint8 }` — flags-combined; negotiation picks highest common capability |
| `0x04` | `ACCEPT` | recipient → initiator | `{ capability: uint8 }` — negotiated level |
| `0x05` | `REJECT` | recipient → initiator | `{ reason: string? }` |
| `0x0A` | `CONFIRM` | both sides after ACCEPT | `{ transcriptHash: bytes[32] }` — SHA-256 of all HELLO + INVITE + ACCEPT frame bytes; both sides MUST verify agreement before entering ACTIVE |
| `0x06` | `BYE` | either direction | `{}` |
| `0x07` | `TYPING` | either in ACTIVE | `{}` — suppressed 3s after last keystroke |
| `0x08` | `DELIVERED` | either in ACTIVE | `{ messageId: bytes[16] }` — RFC 4122 big-endian UUID |
| `0x09` | `MUTE_STATE` | either in ACTIVE | `{ isMuted: bool }` — Phase 3 |
| `0x10` | `TEXT` | either in ACTIVE | `{ messageId: bytes[16], message: string }` — `messageId` RFC 4122 big-endian UUID |
| `0x20` | `AUDIO_FRAME` | either in ACTIVE | opaque Opus packet — Phase 3 |
| `0x30` | `VIDEO_FRAME` | either in ACTIVE | opaque H.264/VP8 — Phase 4 |

**UUID/GUID encoding in MessagePack payloads:** all UUID fields MUST be encoded as `fixext 16` in RFC 4122 big-endian byte order. Do NOT use `Guid.ToByteArray()` — it produces a mixed-endian layout (little-endian `Data1`, little-endian `Data2`/`Data3`, big-endian `Data4`). Instead write each UUID component with `BinaryPrimitives.WriteUInt32BigEndian` / `WriteUInt16BigEndian`, or use a normalizing MessagePack extension that guarantees RFC 4122 byte order. Failure to normalize breaks interoperability with any non-.NET endpoint.

**Encryption handshake (on every connection — no session resumption):**

```
1. Both peers generate ephemeral X25519 key pair
2. Construct HELLO: { ephemeralPublicKey, identityPublicKey (Ed25519), tenantId,
      signature = Ed25519Sign(longTermPrivKey, ephemeralPublicKey || identityPublicKey || tenantId) }
3. Exchange HELLO frames (plaintext)
4. Receiver validates: Ed25519Verify(sender.identityPublicKey, sender.signature)
   → reject if invalid; close stream immediately
5. Receiver validates: sender.identityPublicKey ∈ ICrewRoster.GetCrewAsync(tenant)
   AND sender.tenantId == local tenantId → reject if not enrolled in same tenant
6. sharedSecret = X25519(myEphemeralPrivate, theirEphemeralPublic)
7. sessionKey   = HKDF-SHA256(
       ikm  = sharedSecret,
       salt = "sunfish-crew-comms-v1",
       info = concat(initiatorPeerId.Value, responderPeerId.Value)
   )
8. All frames after HELLO encrypted as:
   [Nonce (12B counter)] ++ ChaCha20Poly1305.Encrypt(sessionKey, nonce, plainFrame)
9. After ACCEPT, both sides independently compute:
   transcriptHash = SHA-256(HELLO_A_bytes || HELLO_B_bytes || INVITE_bytes || ACCEPT_bytes)
   and send CONFIRM { transcriptHash }.
   Mismatch → REJECT + close. Session enters ACTIVE only after both CONFIRMs verified.
```

**No session resumption:** each new `IDuplexStream` connection MUST perform a fresh DH handshake from step 1. Prior session keys MUST be zeroed in memory immediately on `CloseAsync`/`DisposeAsync`. There is no session ticket, no session ID carried across reconnects.

Ed25519 (PeerId long-term key) is used for identity; X25519 is used only for the ephemeral DH exchange. Implementation vehicle: `NSec.Cryptography` (`Algorithm.X25519` + `Algorithm.HkdfSha256` + `Algorithm.ChaCha20Poly1305` + `Algorithm.Ed25519`). Windows CNG does not expose Curve25519 natively; BCL-only X25519 is unreliable across MAUI platforms. `NSec` resolves this with a single cross-platform dependency (libsodium under the hood; MIT license; ~200 KB).

**IDuplexStream threading contract:** `NativeChannelSession` runs a dedicated background reader Task and routes writes from the caller's Task. Both happen concurrently. `IDuplexStream` implementations consumed by this package (`TcpDuplexStream`, `WebSocketDuplexStream`) MUST support concurrent `ReadAsync` + `WriteAsync` from separate Tasks. Verify this contract with the W#30 owner before Phase 1 build begins (see Pre-acceptance audit FAILED condition).

### Signaling state machine

```
Initiator:  IDLE → INVITING → CONFIRMING → ACTIVE → TERMINATED
Recipient:  IDLE → INVITED  → CONFIRMING → ACTIVE → TERMINATED

INVITE timeout: 60s → TERMINATED (reason: InviteTimeout)
BYE: immediate TERMINATED; 2s drain before IDuplexStream.DisposeAsync()
CONFIRM mismatch: TERMINATED (reason: TranscriptMismatch)
```

**Glare resolution (simultaneous-open):** if both peers send INVITE before either sends ACCEPT, both will have pending outbound INVITEs. Resolution: the peer whose `PeerId.Value` is lexicographically lower (UTF-8 byte comparison) yields — it cancels its outbound INVITE, sends `REJECT(reason: "Glare-Yield")`, and transitions back to `INVITED` to await the winning peer's INVITE. The peer with the higher `PeerId.Value` proceeds as initiator. Both sides MUST implement this rule identically; no negotiation needed.

### `foundation-channels` contract surface

```csharp
namespace Sunfish.Foundation.Channels;

[Flags]
public enum ChannelCapability : byte
{
    None  = 0,
    Text  = 1 << 0,
    Audio = 1 << 1,
    Video = 1 << 2,
}

public enum PresenceStatus { Offline, Available, Busy }

public sealed record CrewPresence
{
    public required PeerId         Peer        { get; init; }
    public required TenantId       Tenant      { get; init; }
    public required string         DisplayName { get; init; }
    public required ChannelCapability Caps     { get; init; }
    public required PresenceStatus Status      { get; init; }
    public required TransportTier  Via         { get; init; }
    public required DateTimeOffset LastSeenAt  { get; init; }
}

public sealed record CrewMember
{
    public required PeerId Peer        { get; init; }
    public required string DisplayName { get; init; }
}

public interface ICrewRoster
{
    Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct);
}

public enum ChannelSessionState   { Connecting, Active, Terminated }
public enum ChannelTerminationReason { LocalBye, RemoteBye, InviteTimeout, TransportError }

public interface IChannelSession : IAsyncDisposable
{
    PeerId               Peer       { get; }
    ChannelCapability    Capability { get; }
    ChannelSessionState  State      { get; }

    /// <summary>
    /// Completes when the session reaches TERMINATED state.
    /// Await to observe <see cref="ChannelTerminationReason"/> without a synchronous event handler.
    /// </summary>
    Task<ChannelTerminationReason> Completed { get; }

    Task                     SendTextAsync(string message, CancellationToken ct);

    /// <summary>
    /// Single-consumer only. Enumerating from multiple consumers concurrently is undefined behavior;
    /// implementations MAY throw <see cref="InvalidOperationException"/>.
    /// </summary>
    IAsyncEnumerable<string> ReceiveTextAsync(CancellationToken ct);

    // Phase 3 stubs — throw NotSupportedException if Capability does not include ChannelCapability.Audio.
    // Implementations MUST NOT silently no-op; callers MUST check Capability before invoking.
    Task                                   SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct);

    /// <summary>
    /// Sends BYE, drains pending frames (up to 2s), then completes.
    /// If DisposeAsync is called without a prior CloseAsync, a best-effort BYE is sent fire-and-forget.
    /// </summary>
    Task CloseAsync(CancellationToken ct);
}

public interface IChannelInvitation
{
    PeerId            FromPeer            { get; }
    ChannelCapability OfferedCapabilities { get; }   // flags-combined; caller inspects individual bits
    Task<IChannelSession> AcceptAsync(CancellationToken ct);
    Task RejectAsync(string? reason, CancellationToken ct);
}

public interface IChannelProvider
{
    ChannelCapability Capabilities { get; }

    Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct);

    /// <param name="preferredCapabilities">
    /// Flags-combined value indicating desired capabilities. Implementation selects the highest
    /// common capability between this value and the remote peer's advertised capabilities.
    /// Use <see cref="ChannelCapability.Text"/> for Phase 1 text-only sessions.
    /// </param>
    Task<IChannelSession>             OpenAsync(TenantId tenant, PeerId peer,
                                                ChannelCapability preferredCapabilities,
                                                CancellationToken ct);

    /// <summary>
    /// Backed by a bounded Channel of capacity 16. Incoming INVITEs dropped when full;
    /// a <c>ChannelInviteDropped</c> audit event is emitted on each drop.
    /// Callers MUST process each <see cref="IChannelInvitation"/> promptly (accept or reject).
    /// </summary>
    IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct);
}
```

### `blocks-crew-comms` internal structure

```
NativeChannelProvider : IChannelProvider
  ├── PresenceBus          — heartbeat timer (30s) + TTL-eviction roster (45s) + in-session keepalive (20s idle)
  │                          + speculative relay HELLO probe for roster peers not seen via mDNS (Phase 2)
  ├── SessionListener      — System.Threading.Channels.Channel<IChannelInvitation> bounded(capacity:16)
  │                          drops incoming INVITEs when full; emits ChannelInviteDropped audit event
  ├── SessionInitiator     — OpenAsync → HELLO exchange (with Ed25519 sig verify + roster check)
  │                          → INVITE → wait ACCEPT (60s timeout) → exchange CONFIRM → ACTIVE
  ├── EncryptionHandshake  — ephemeral X25519 + HKDF-SHA256 session-key derivation; Ed25519 sign/verify;
  │                          session key zeroed on close; no resume
  └── FrameProtocol        — length-prefix + MessagePack encode/decode; RFC 4122 UUID normalization

NativeChannelSession : IChannelSession
  ├── holds IDuplexStream (owned; disposed on CloseAsync; best-effort BYE on DisposeAsync without prior Close)
  ├── dedicated reader Task — decrypt + deserialize + route frames (concurrent with writer; IDuplexStream must support)
  ├── routes TEXT → Channel<string> (backing ReceiveTextAsync; single-consumer contract enforced)
  ├── routes AUDIO_FRAME → JitterBuffer → Channel<ReadOnlyMemory<byte>> (Phase 3)
  ├── Completed property   — TaskCompletionSource<ChannelTerminationReason>; set on BYE / error / timeout
  └── JitterBuffer — adaptive 20–80ms depth, 40ms default

InMemoryCrewRoster : ICrewRoster  (stub for Phase 1; replaced by identity-layer impl later)
```

**DI registration:**
```csharp
// Anchor MauiProgram.cs
services.AddSunfishCrewComms(roster =>
{
    // caller supplies ICrewRoster implementation
    roster.AddInMemory(/* seed entries */);
});
```

### Phase delivery plan

| Phase | Scope | Transport tier | Key additions |
|---|---|---|---|
| 1 | LAN text chat | mDNS + TCP (Tier 1) | `foundation-channels` scaffold + `blocks-crew-comms` NativeChannelProvider + PresenceBus + EncryptionHandshake + TEXT/TYPING/DELIVERED framing + Anchor UI wiring |
| 2 | Cross-network text | Bridge relay (Tier 3) | Zero channel code change — `ITransportSelector` handles tier fallback; integration test with relay URL |
| 3 | Audio | Any tier | `Concentus` Opus encode/decode; `AUDIO_FRAME` + `MUTE_STATE` activated; JitterBuffer (40ms default); push-to-talk default; OS-level AEC for always-on (Phase 3.1) |
| 4 | Video | Any tier | Follow-on workstream; H.264/VP8; `VIDEO_FRAME` activated; SFU evaluation if multi-party needed |

**Phase 1 acceptance criteria:**
1. Two Anchor instances on the same LAN see each other in the crew roster within 30s of startup.
2. Initiating crew member sends INVITE; recipient surfaces an `IChannelInvitation` via `ListenAsync`.
3. After `AcceptAsync`, text messages flow bidirectionally with `MessageId` populated.
4. `DELIVERED` ack received by sender for each TEXT frame.
5. `TYPING` indicator visible to remote peer within 200ms of keystroke.
6. All frames ChaCha20-Poly1305 encrypted; decryption fails fast on tampered bytes.
7. `BYE` from either side cleans up `IDuplexStream` within 2s.
8. Presence roster evicts a stopped node within 45s.

**Phase 2 acceptance criteria:**
1. Phase 1 criteria pass with nodes on separate networks (Bridge relay URL configured).
2. `ITransportSelector` selects `TransportTier.ManagedRelay`; audit event `TransportFallbackToRelay` emitted.
3. Bridge relay log shows no plaintext crew message content.

---

## Consequences

### Positive

- Crew communication ships as a first-class, default-installed Anchor feature without requiring domain blocks.
- Transport layer (ADR 0061) validated end-to-end under real workload across all three tiers.
- Forward-secret E2E encryption via BCL primitives only; relay posture (ADR 0031 ciphertext-only) preserved.
- Protocol is incrementally extensible: Phase 3 audio and Phase 4 video add message types without breaking Phase 1 sessions.
- Provider interface enables future `compat-zoom` / `compat-teams` without modifying Anchor or `foundation-channels`.
- `PresenceStatus.Busy` future-proofs the UI against a breaking change when multi-session or DND lands.

### Negative

- Two new packages to scaffold, CI-wire, and document.
- `ICrewRoster` is a stub in Phase 1 — must be replaced with a real tenant identity implementation before multi-user deployment. Phase 1 is single-tenant, manually seeded.
- `NSec.Cryptography` (~200 KB, libsodium-backed) is a new library dependency in `blocks-crew-comms`. It is MIT-licensed and well-audited; the alternative (P-256 fallback to maintain BCL-only) would weaken the stated security properties. This is the correct trade-off.
- `Concentus` Opus encoder (~5–10ms per 20ms frame) adds Phase 3 CPU overhead on lower-end devices. Acceptable for desktop/iOS; validate on Windows MAUI ARM targets.

### Trust impact / Security & privacy

- All channel traffic is E2E encrypted before leaving the sender's process. The Bridge relay (Tier 3) handles only ciphertext, preserving ADR 0031's tenant-data-isolation posture.
- Forward secrecy: each session derives a fresh key from ephemeral DH material. No session resumption; prior session keys are zeroed on `CloseAsync`/`DisposeAsync`. Compromise of `PeerId` Ed25519 long-term key does not expose past sessions.
- **HELLO authentication:** ephemeral DH public keys are signed with the sender's Ed25519 long-term key. Receivers MUST verify the signature before computing the shared secret; reject and close immediately on failure. This prevents a relay or network attacker from substituting a different ephemeral public key (classic X25519 MitM).
- **HEARTBEAT authentication:** each HEARTBEAT carries an Ed25519 signature over `(peerId || tenantId || caps || timestamp)`. Receivers MUST verify before accepting presence updates; reject on failure. This prevents roster poisoning from unauthenticated broadcast frames.
- **Capability negotiation integrity:** both sides send `CONFIRM { transcriptHash }` after ACCEPT. Mismatch → reject + close. This prevents a downgrade attack that tricks one side into using a lower capability than the other.
- **Tenant binding:** HELLO includes `tenantId`; receiver MUST verify `sender.tenantId == local.tenantId` AND `sender.identityPublicKey ∈ ICrewRoster`. Reject if either check fails. This closes the cross-tenant HELLO injection vector.
- `MessageId` UUIDs are generated locally; they must not encode timestamps or device fingerprints (use `Guid.NewGuid()`, not time-based UUIDs).
- `HEARTBEAT` frames include `TenantId`; peers MUST reject heartbeats for tenants they do not participate in.

---

## Compatibility plan

No existing packages are modified. Two new packages added:

| Package | Action |
|---|---|
| `packages/foundation-channels/` | NEW — contracts only; no behavioral change to existing packages |
| `packages/blocks-crew-comms/` | NEW — native impl; registered in Anchor by default |
| `packages/foundation-transport/` | READ ONLY — consumed; no changes in Phase 1 |
| `accelerators/anchor/` | MauiProgram.cs addition: `services.AddSunfishCrewComms(...)` |

---

## Implementation checklist

**Phase 1 — LAN text chat**

- [ ] Scaffold `packages/foundation-channels/Sunfish.Foundation.Channels.csproj` — IsPackable, deps on foundation + foundation-transport + foundation-multitenancy
- [ ] Implement `ChannelCapability`, `PresenceStatus`, `CrewPresence`, `CrewMember` value types
- [ ] Implement `ICrewRoster`, `IChannelSession`, `IChannelInvitation`, `IChannelProvider` interfaces
- [ ] Scaffold `packages/blocks-crew-comms/Sunfish.Blocks.CrewComms.csproj` — IsPackable, deps on foundation-channels + foundation-transport + foundation-multitenancy
- [ ] Implement `FrameProtocol` — length-prefix framing + MessagePack encode/decode for all v1 message types; RFC 4122 big-endian UUID encoding for all UUID fields (not `Guid.ToByteArray()`)
- [ ] Implement `EncryptionHandshake` — ephemeral X25519 + HKDF-SHA256 + ChaCha20-Poly1305 via `NSec.Cryptography`; Ed25519 sign (HELLO + HEARTBEAT) + verify; roster membership check on HELLO; CONFIRM transcript-hash exchange; session key zeroed on close; no session resume
- [ ] Implement `PresenceBus` — 30s heartbeat timer with signed HEARTBEAT frames; `ICrewRoster.GetCrewAsync` for peer list; TTL-eviction at 45s; 20s in-session keepalive; mDNS cache fast-path via `ITransportSelector`
- [ ] Implement `NativeChannelSession` — dedicated reader Task (concurrent with writer; verify `IDuplexStream` threading contract first); TEXT/TYPING/DELIVERED routing; `Completed` TaskCompletionSource; `CloseAsync` with 2s drain + BYE; `DisposeAsync` best-effort BYE
- [ ] Implement `SessionInitiator` — `OpenAsync` → HELLO exchange (sig verify + roster check) → INVITE (flags-combined `ChannelCapability`) → wait ACCEPT (60s timeout) → CONFIRM exchange → ACTIVE; glare detection via PeerId comparison
- [ ] Implement `SessionListener` — `ListenAsync` backed by `Channel.CreateBounded<IChannelInvitation>(16)`; emit `ChannelInviteDropped` audit event on drop; CONFIRM exchange on `AcceptAsync`
- [ ] Implement `InMemoryCrewRoster` — configurable seed; used in Anchor Phase 1 and tests
- [ ] Implement `NativeChannelProvider` — wires all internal components; registers as `IChannelProvider`
- [ ] Add `AddSunfishCrewComms(Action<CrewCommsBuilder>)` DI extension
- [ ] Wire into `accelerators/anchor/MauiProgram.cs` — default-installed
- [ ] Unit tests: FrameProtocol round-trip + UUID encoding; EncryptionHandshake shared-secret agreement + HELLO sig verify + CONFIRM hash; PresenceBus 45s TTL eviction + 20s keepalive; signaling state machine transitions including glare; INVITE timeout; ListenAsync drop-when-full
- [ ] Integration test: two in-process `NativeChannelProvider` instances exchange text messages end-to-end (mocked `IDuplexStream` pair — no real network needed for unit suite)
- [ ] `apps/docs/blocks/crew-comms/overview.md`
- [ ] Ledger flip W#45 Phase 1 row

**Phase 2 — Cross-network text (no new channel code)**

- [ ] Integration test: Phase 1 test repeated with `BridgeRelayPeerTransport` as the transport tier
- [ ] Verify `TransportFallbackToRelay` audit event emitted (ADR 0061 §"Audit emission")
- [ ] Confirm Bridge relay log shows no plaintext message content
- [ ] Verify relay-tier presence uses lazy model: `PresenceStatus.Available` set only for peers that completed HELLO in the last 45s; no active per-peer probe over relay at heartbeat interval

**Phase 3 — Audio (separate hand-off)**

- [ ] Add `Concentus` NuGet to `blocks-crew-comms`
- [ ] Implement `JitterBuffer` — adaptive 20–80ms depth, 40ms default, configurable via `CrewCommsAudioOptions`
- [ ] Implement push-to-talk input surface in Anchor UI
- [ ] Activate `AUDIO_FRAME` + `MUTE_STATE` message types
- [ ] OS-level AEC integration (`AVAudioSession` / Windows `AudioGraph`) for always-on Phase 3.1
- [ ] Validate `Concentus` throughput on Windows MAUI ARM target

---

## Open questions

1. **~~X25519 in .NET BCL~~ — RESOLVED 2026-05-04.** Windows CNG does not expose Curve25519; BCL-only X25519 is unreliable across MAUI platforms. **Decision: `NSec.Cryptography` (MIT, ~200 KB, libsodium-backed).** `EncryptionHandshake` uses `NSec.Cryptography.Algorithm.X25519` + `Algorithm.HkdfSha256` + `Algorithm.ChaCha20Poly1305`. Add `NSec.Cryptography` to `blocks-crew-comms.csproj`; document in `EncryptionHandshake.cs` XML doc. Revisit trigger updated accordingly.

2. **~~Relay-tier heartbeat cost~~ — RESOLVED 2026-05-04 + amended by council 2026-05-04.** Active per-peer relay probing at 30s heartbeat is cost-prohibitive at scale. **Decision: lazy relay presence with speculative bootstrap.** `PresenceStatus.Available` only set for peers that completed a HELLO handshake within the last 45s. Relay-tier peers with no recent session show `PresenceStatus.Offline` until they connect. `PresenceBus` resolves the relay bootstrap chicken-and-egg by performing speculative relay HELLO probes for known roster peers not seen via mDNS (10s timeout per probe; max once per heartbeat period per peer). Phase 2 integration test includes a verification step (see Implementation checklist).

3. **`ICrewRoster` → tenant identity system wiring** — `InMemoryCrewRoster` is a Phase 1 stub. Wire to the actual tenant identity system before multi-user Anchor deployment. **Halt-condition for production; not blocking Phase 1 LAN demo.**

4. **mDNS capability TXT records** — extending `MdnsPeerTransportOptions` with `ExtraTxtRecords` dictionary to carry `caps=text` gives instantaneous LAN presence without a heartbeat round-trip. Minor `foundation-transport` addition — coordinate with W#30 owner during Phase 1 build. **Not blocking.**

5. **Message persistence** — `IChannelSession` does not persist messages. Phase 1 stores in memory only (lost on restart). AP-class durable local storage is a follow-on workstream. **Not a Phase 1 blocker; document limitation clearly in `apps/docs`.**

---

## Revisit triggers

- `foundation-transport` (W#30) API changes that alter `ITransportSelector`, `IPeerTransport`, or `IDuplexStream` signatures — evaluate impact on `EncryptionHandshake` + `FrameProtocol`.
- .NET version bump that provides first-class `NSec`-equivalent X25519 support in BCL without platform inconsistency — evaluate migration from `NSec.Cryptography` to BCL-only to drop the library dependency.
- First compat adapter engagement (Zoom, Teams) — review `IChannelProvider` surface for gaps; write compat-adapter ADR at that point.
- Phase 3 audio ships and AEC quality is inadequate on a target platform — revisit push-to-talk default vs always-on strategy.
- Multi-party (group) session request — current 1:1 signaling does not extend to SFU; requires new ADR.

---

## References

### Predecessor and sister ADRs

- [ADR 0061](./0061-three-tier-peer-transport.md) — `foundation-transport`: `ITransportSelector`, `IPeerTransport`, `IDuplexStream`, `TransportTier`. Direct dependency.
- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge relay: ciphertext-only posture preserved by this ADR's E2E encryption.
- [ADR 0052](./0052-bidirectional-messaging-substrate.md) — `blocks-messaging`: async durable email/SMS threads; confirmed no collision with real-time crew comms.
- [ADR 0028](./0028-crdt-and-local-first-sync.md) — AP record class model; message persistence strategy for follow-on phase.
- [ADR 0046](./0046-key-management-and-recovery.md) — key management substrate; `PeerId` Ed25519 key lifecycle governs the long-term identity used in the DH handshake.

### Roadmap and specifications

- W#45 intake: `icm/00_intake/output/2026-05-04_crew-comms-intake.md`
- W#45 Stage 01 Discovery: `icm/01_discovery/output/2026-05-04_crew-comms-discovery.md`
- Architecture paper §20.7 — Zone A accelerator (Anchor local-first desktop)

### Existing code / substrates

- `packages/foundation-transport/ITransportSelector.cs` — `SelectAsync(PeerId, ct)` entry point
- `packages/foundation-transport/IPeerTransport.cs` — `ConnectAsync(PeerId, ct)` → `IDuplexStream`
- `packages/foundation-transport/IDuplexStream.cs` — raw byte stream; framing added by this ADR
- `packages/foundation-transport/TransportTier.cs` — `LocalNetwork` / `MeshVpn` / `ManagedRelay`
- `packages/foundation-transport/Mdns/MdnsPeerTransport.cs` — Tier-1 peer cache (presence fast-path)
- `packages/federation-common/` — `PeerId` definition

### External

- RFC 7748 — Elliptic Curves for Security (X25519/X448)
- RFC 5869 — HMAC-based Key Derivation Function (HKDF)
- RFC 8439 — ChaCha20 and Poly1305 for IETF Protocols
- Opus codec: https://opus-codec.org — ITU-T G.718 speech codec; built-in PLC
- Concentus (pure-managed Opus): https://github.com/lostromb/concentus — MIT license
- Slack Engineering: "How We Built Huddles" — push-to-talk + AEC lessons
- Zoom Engineering Blog: "How Zoom's Video Architecture Works" — adaptive bitrate + tier fallthrough

---

## Pre-acceptance audit

- [x] **AHA pass.** Option A (single package) and Option C (extend blocks-messaging) considered and rejected above.
- [x] **FAILED conditions.** Kill trigger: if `IDuplexStream` does not support concurrent `ReadAsync` + `WriteAsync` from separate Tasks, the dedicated reader/writer Task design must change — verify with W#30 owner before Phase 1 build begins. Kill trigger: CONFIRM transcript-hash mismatch during testing indicates a FrameProtocol serialization bug — halt and fix before any security property claims hold.
- [x] **Rollback strategy.** `foundation-channels` and `blocks-crew-comms` are new packages; rollback = remove packages + revert `MauiProgram.cs` addition. No existing packages modified.
- [x] **Confidence level.** HIGH — Open Questions §1 (NSec.Cryptography adopted) and §2 (lazy presence + speculative bootstrap) resolved. Council amendments applied 2026-05-04 (18 Required findings addressed). Phase 1 LAN text is HIGH confidence; Phase 2 relay is HIGH confidence given speculative HELLO bootstrap resolution.
- [x] **Cited-symbol verification.** `ITransportSelector`, `IPeerTransport`, `IDuplexStream`, `TransportTier`, `PeerId`, `MdnsPeerTransport` — all verified present in `packages/foundation-transport/` staged files. `TenantId` — present in `packages/foundation-multitenancy/`. `ICrewRoster`, `IChannelProvider`, `IChannelSession`, `IChannelInvitation`, `ChannelCapability`, `PresenceStatus`, `CrewPresence`, `CrewMember`, `NativeChannelProvider`, `InMemoryCrewRoster` — **introduced by this ADR**; marked in Implementation checklist. `NSec.Cryptography` — MIT-licensed NuGet, not yet in solution; must be added to `blocks-crew-comms.csproj`.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): Open Questions §3–§5 explicit; §1–§2 resolved. AP-3 (vague phases): Phase 1 has 8-point acceptance criteria. AP-11 (zombie project): Revisit triggers named. AP-21 (cited-symbol drift): verified above. AP-15 (premature precision): wire protocol byte assignments are stable commitments — intentional.
- [x] **Revisit triggers.** Named in §Revisit triggers.
- [x] **Cold Start Test.** Implementation checklist has 17 discrete Phase 1 steps; each is independently verifiable. IDuplexStream threading contract and CONFIRM hash logic are explicit decision gates.
- [x] **Sources cited.** RFCs cited for X25519, HKDF, ChaCha20-Poly1305. Concentus repo + Opus codec spec cited. ADR 0061 §"Audit emission" cited for relay audit event. RFC 4122 cited for UUID encoding. Ed25519 signing per ADR 0046 key management substrate.
- [x] **Council review.** 4-perspective adversarial council dispatched 2026-05-04 (Outside Observer, Pessimistic Risk Assessor, Skeptical Implementer, Security/Crypto). All 18 Required findings applied in this ADR version. 0 Required findings unaddressed.

---

## Amendment A1 — Wire-encoding ratification: transcript-hash canonical form, TenantId encoding, PeerId encoding

**Amendment date:** 2026-05-05
**Authors:** XO research session
**Workstream:** W#45 P2 (unblocking COB question `cob-question-2026-05-04T21-22Z-w45-p2-adr-0076-a1.md`)
**Pipeline variant:** `sunfish-api-change` (mechanical wire-encoding clarification; no new types introduced)

---

### A1.1 Context

W#45 Phase 2 (`blocks-crew-comms` + `EncryptionHandshake`) was built locally. The pre-merge
council returned **RETURN-FOR-REWORK** on three wire-encoding claims in the base ADR that
the implementation correctly diverged from. This amendment ratifies the implementation's
choices, which are superior to the base ADR's original text.

The four items requiring ratification:

1. **Transcript-hash canonical form** — the base ADR specifies "SHA-256 of all HELLO + INVITE + ACCEPT frame bytes." The implementation computes a reduced form over the cryptographically significant fields only; this amendment also binds `presence.caps` from HELLO into the HELLO signable to close a relay-MitM capability-downgrade vector.
2. **TenantId encoding in signables** — the base ADR claims `tenantId: uuid` (16 bytes RFC 4122). `TenantId(string Value)` is string-backed with no UUID semantics; UUID encoding is not implementable without a breaking multi-tenant type change.
3. **PeerId encoding in HEARTBEAT** — the base ADR claims `peerId: uuid`. `PeerId.From(PrincipalId)` is the base64url encoding of the raw 32-byte Ed25519 pubkey; the implementation signs over the raw 32 bytes (specifically `PrincipalId.AsSpan()`).
4. **Endianness convention** — the base ADR does not state endianness for multi-byte signable integers; this amendment makes it explicit.

---

### A1.2 Decision drivers

1. **Robustness over full-frame-bytes hashing.** Full MessagePack-serialized frame bytes are sensitive to library-level key-ordering, codec version, and frame-delimiter choices that may evolve across Sunfish versions. A reduced canonical form over the fixed-size cryptographic fields is stable across serialization changes. The `presence.caps` field in HELLO is additionally bound into the HELLO signable to prevent a relay-MitM from clamping advertised capabilities (causing the remote peer to never offer audio/video in INVITE without either side detecting the attack via CONFIRM).
2. **`TenantId` is string-backed.** `Sunfish.Foundation.Assets.Common.TenantId(string Value)` has `string Value` as its only field (verified on `origin/main`). Encoding as UUID-16 is structurally impossible without introducing a parallel UUID-backed `TenantId` variant — a breaking change touching every multi-tenant package. UTF-8 bytes with a length prefix is the correct representation for a variable-length string field in a binary signing context. The length prefix is structurally redundant in HELLO (tenantBytes is the trailing field), but required in HEARTBEAT (where tenantBytes is flanked by fixed-length peerId and caps/timestamp); the unified encoding is intentional to keep `TenantId` canonicalization identical across all signables and prevent drift.
3. **`PeerId` is the base64url form of a 32-byte raw pubkey.** `PeerId.From(PrincipalId)` is defined as `principal.ToBase64Url()` (verified in `packages/federation-common/PeerId.cs`). Signing over the raw 32 bytes directly (the underlying `PrincipalId.AsSpan()`) is simpler, avoids double-encoding, and is consistent with HELLO's `identityPublicKey: bytes[32]` which is already the raw key. Implementers MUST use `PrincipalId.AsSpan()` (32 bytes), NOT `System.Text.Encoding.UTF8.GetBytes(PeerId.Value)` (43 bytes of base64url string) — the latter would silently diverge from every other peer on the network.
4. **Endianness must be explicit.** The base ADR's framing header uses little-endian (`Length: 4B LE`) but multi-byte integers in MessagePack payloads (including `timestamp: int64`) are big-endian per MessagePack spec. Without an explicit convention, an implementer applying framing-header endianness to signable inputs would produce a divergent HEARTBEAT signature that silently passes round-trip tests but fails interoperability with any second client.

---

### A1.3 Decisions

#### A1 — Transcript-hash canonical form (CONFIRM frame)

**Replaces** base ADR §CONFIRM table row and §Handshake step 9.

The CONFIRM `{ transcriptHash: bytes[32] }` MUST be computed as:

```
transcriptHash = SHA-256(
    ephemA[32]                                     // HELLO_A.ephemeralPublicKey
    || idA[32]                                     // HELLO_A.identityPublicKey
    || ephemB[32]                                  // HELLO_B.ephemeralPublicKey
    || idB[32]                                     // HELLO_B.identityPublicKey
    || uint32BE(len(tenantBytes)) || tenantBytes   // TenantId.Value as length-prefixed UTF-8
    || negotiatedCap[1]                            // ACCEPT.capability (uint8)
    || presenceCapsA[1]                            // HELLO_A.presence.caps (uint8)
    || presenceCapsB[1]                            // HELLO_B.presence.caps (uint8)
)
```

Where:
- `ephemA` / `idA` = the initiator's ephemeral X25519 public key + Ed25519 identity public key from its HELLO frame.
- `ephemB` / `idB` = the responder's ephemeral X25519 public key + Ed25519 identity public key from its HELLO frame.
- `tenantBytes` = `System.Text.Encoding.UTF8.GetBytes(TenantId.Value)`.
- `negotiatedCap` = the single `capability: uint8` byte from the ACCEPT frame.
- `presenceCapsA` = the `presence.caps` uint8 field from HELLO_A (initiator's advertised capability bitmask).
- `presenceCapsB` = the `presence.caps` uint8 field from HELLO_B (responder's advertised capability bitmask).

**Rationale for NOT using full frame bytes:** MessagePack key ordering is serializer-dependent (dictionary vs struct codec); the `presence: PresenceHeartbeat` field in HELLO carries TTL/timestamp state irrelevant to session integrity; INVITE has only `capabilities: uint8` subsumed by `negotiatedCap`. The reduced form includes every field that affects session security (shared DH material and tenant binding), capability negotiation, and capability advertisement. Both `presence.caps` values are bound so a relay-MitM cannot clamp either peer's advertised capability bitmask without CONFIRM detecting the tamper.

**Total input size:** 32 + 32 + 32 + 32 + 4 + len(tenantBytes) + 1 + 1 + 1 = 135 + len(tenantBytes) bytes (≤ 200 bytes for any `TenantId.Value` ≤ 63 UTF-8 bytes, which covers all real tenant identifiers).

---

#### A2 — TenantId encoding in HELLO and HEARTBEAT signables

**Replaces** base ADR wire protocol table rows for HELLO and HEARTBEAT, and §Handshake steps 2 and security note for HEARTBEAT.

`tenantId` in ALL binary signables (HELLO signature input, HEARTBEAT signature input, and CONFIRM transcript hash input per A1) MUST be encoded as:

```
uint32BE(len(tenantBytes)) || tenantBytes
```

Where `tenantBytes = System.Text.Encoding.UTF8.GetBytes(TenantId.Value)`.

The `tenantId` field in the **MessagePack frame payload** itself is also updated from `uuid` to `bytes`:

| Frame | Field | Old claim | Ratified encoding |
|---|---|---|---|
| HELLO payload | `tenantId` | `uuid` (16-byte RFC 4122) | `bytes` — `uint32BE(len) \|\| UTF-8(TenantId.Value)` |
| HELLO signable | `tenantId` | raw 16-byte UUID | `uint32BE(len(tenantBytes)) \|\| tenantBytes` |
| HEARTBEAT payload | `tenantId` | `uuid` (16-byte RFC 4122) | `bytes` — `uint32BE(len) \|\| UTF-8(TenantId.Value)` |
| HEARTBEAT signable | `tenantId` | raw 16-byte UUID | `uint32BE(len(tenantBytes)) \|\| tenantBytes` |

**Implementation note:** The length prefix eliminates ambiguity in the HEARTBEAT signable where `tenantId` is flanked by `peerId[32]` (fixed) and `caps[1] || timestamp[8]` (fixed). Without the prefix, the input boundaries would be inferred by position-from-tail, which is fragile.

---

#### A3 — PeerId encoding in HEARTBEAT signable and payload

**Replaces** base ADR wire protocol table row for HEARTBEAT.

`peerId` in the HEARTBEAT payload and signable MUST be the **raw 32-byte Ed25519 identity public key** (the underlying `PrincipalId` bytes), NOT a UUID representation.

| Frame | Field | Old claim | Ratified encoding |
|---|---|---|---|
| HEARTBEAT payload | `peerId` | `uuid` (16-byte RFC 4122) | `bytes[32]` — raw Ed25519 identity public key |
| HEARTBEAT signable | `peerId` | raw 16-byte UUID | `bytes[32]` — raw Ed25519 identity public key |

The resolved HEARTBEAT signable is:

```
signature = Ed25519Sign(longTermPrivKey,
    peerId_raw[32]                                 // raw 32-byte Ed25519 identity pubkey
    || uint32BE(len(tenantBytes)) || tenantBytes   // TenantId length-prefixed UTF-8
    || caps[1]                                     // uint8
    || timestamp_BE[8]                             // int64 big-endian
)
```

**Mapping from `PeerId` to signing input:** `PeerId` is `base64url(principalId.Bytes[32])`; the signing input is the 32 bytes returned by `PrincipalId.AsSpan()` directly. Implementers MUST call `PrincipalId.AsSpan()` to obtain these 32 bytes — NOT `System.Text.Encoding.UTF8.GetBytes(PeerId.Value)`, which returns 43 bytes of base64url string and would silently produce a divergent signature on every peer in the network. The `PeerId.Value` string is never used in any signing input.

---

### A1.4 Updated wire protocol table (A1-ratified rows)

Replaces base ADR §Wire protocol frame table rows `0x01`, `0x02`, `0x0A`:

| Byte | Name | Payload (ratified) |
|---|---|---|
| `0x01` | `HELLO` | `{ ephemeralPublicKey: bytes[32], identityPublicKey: bytes[32], tenantId: bytes (uint32BE-len-prefixed UTF-8 of TenantId.Value), signature: bytes[64], presence: PresenceHeartbeat }` — `signature` = Ed25519(longTermPrivKey, ephemeralPublicKey \|\| identityPublicKey \|\| uint32BE(len(tenantBytes)) \|\| tenantBytes \|\| presence.caps[1]) |
| `0x02` | `HEARTBEAT` | `{ peerId: bytes[32] (raw Ed25519 identity pubkey), tenantId: bytes (uint32BE-len-prefixed UTF-8 of TenantId.Value), caps: uint8, timestamp: int64, signature: bytes[64] }` — `signature` = Ed25519(longTermPrivKey, peerId_raw[32] \|\| uint32BE(len(tenantBytes)) \|\| tenantBytes \|\| caps[1] \|\| timestamp_BE[8]) |
| `0x0A` | `CONFIRM` | `{ transcriptHash: bytes[32] }` — SHA-256 over (ephemA[32] \|\| idA[32] \|\| ephemB[32] \|\| idB[32] \|\| uint32BE(len(tenantBytes)) \|\| tenantBytes \|\| negotiatedCap[1] \|\| presenceCapsA[1] \|\| presenceCapsB[1]); both sides MUST verify agreement before entering ACTIVE |

---

### A1.5 Updated handshake steps

Replaces base ADR §Handshake step 2 and step 9.

**Step 2 (HELLO construction):**
```
2. Construct HELLO: {
       ephemeralPublicKey,
       identityPublicKey (Ed25519 long-term key),
       tenantId = uint32BE(len(tenantBytes)) || tenantBytes,   // A1-ratified
       signature = Ed25519Sign(longTermPrivKey,
           ephemeralPublicKey || identityPublicKey
           || uint32BE(len(tenantBytes)) || tenantBytes         // A1-ratified
           || presence.caps[1])                                 // A1 Q1 — caps bound in signable
   }
```

**Step 9 (CONFIRM computation):**
```
9. After ACCEPT, both sides independently compute:
   tenantBytes    = UTF8.GetBytes(localTenantId.Value)
   transcriptHash = SHA-256(
       HELLO_A.ephemeralPublicKey[32]
       || HELLO_A.identityPublicKey[32]
       || HELLO_B.ephemeralPublicKey[32]
       || HELLO_B.identityPublicKey[32]
       || uint32BE(len(tenantBytes)) || tenantBytes             // A1-ratified
       || ACCEPT.capability[1]                                  // A1-ratified: negotiatedCap only
       || HELLO_A.presence.caps[1]                             // A1 Q1 — caps bound to detect downgrade
       || HELLO_B.presence.caps[1]                             // A1 Q1 — caps bound to detect downgrade
   )
   and send CONFIRM { transcriptHash }.
   Mismatch → REJECT + close. Session enters ACTIVE only after both CONFIRMs verified.
```

---

### A1.6 §A0 self-audit (per ADR 0069 discipline)

This amendment introduces **no new `Sunfish.*` types**. All changes are to wire-encoding
descriptions and signing-input specifications.

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId(string Value)` | Existing | yes — string-backed, no UUID field; length-prefix encoding is correct |
| `Sunfish.Federation.Common.PeerId(string Value)` | Existing | yes — `packages/federation-common/PeerId.cs`; `.Value` is base64url; raw bytes come from underlying `PrincipalId` |
| `Sunfish.Foundation.Crypto.PrincipalId` | Existing | yes — `PeerId.From(PrincipalId)` confirmed in `PeerId.cs` |

No §A0.1 negative-existence check needed (no new types introduced).
No §A0.2 false-positive risk: existing types are consumed at their verified signatures.
§A0.3 structural correctness: `uint32BE` = `BinaryPrimitives.WriteUInt32BigEndian`; standard .NET pattern verified.

---

### A4 — Endianness convention

The ADR uses two distinct endianness contexts that MUST NOT be mixed:

| Context | Endianness | Scope |
|---|---|---|
| Framing header `Length` field | Little-endian (LE) | 4-byte frame length prefix on the wire |
| Signable integer inputs | Big-endian (BE) | All multi-byte integers in Ed25519Sign or SHA-256 inputs (`uint32BE` length prefixes, `int64` timestamps) |
| MessagePack integer payloads | Big-endian (BE) | Per MessagePack spec; applies to `timestamp`, capability masks, etc. |

**Implementation guidance:**

- Use `BinaryPrimitives.WriteUInt32BigEndian(span, value)` (NOT `LE`) for `uint32BE(len(tenantBytes))` in HELLO, HEARTBEAT, and CONFIRM signing inputs.
- Use `BinaryPrimitives.WriteInt64BigEndian(span, value)` (NOT `LE`) for `timestamp_BE[8]` in the HEARTBEAT signable.
- The framing `Length: 4B LE` is the outer length prefix consumed before MessagePack deserialization — it does not determine endianness of any field inside the payload.

**Why this can go silently wrong:** round-trip tests on a single implementation pass regardless of endianness (the same codec writes and reads). Cross-peer interoperability fails silently on the wire. Known-answer tests (see §A1.7) catch this class of error by comparing against a fixed expected byte sequence.

---

### A1.7 Implementation checklist (W#45 P2 unblock)

- [ ] `EncryptionHandshake` — update HELLO construction: (a) use `uint32BE(len) || UTF8(tenantId.Value)` in the signable (replaces uuid-16 encoding); (b) include `presence.caps[1]` as the trailing byte of the Ed25519 signable input (§A1.5 step 2)
- [ ] `EncryptionHandshake` — update CONFIRM hash to use reduced-form input (A1.3 / A1.5 step 9), NOT full frame bytes; include `HELLO_A.presence.caps[1] || HELLO_B.presence.caps[1]` as the trailing two bytes of the SHA-256 input
- [ ] `PresenceBus` — update HEARTBEAT construction: use `PrincipalId.AsSpan()` (32 bytes) for `peerId_raw[32]` in the signable (NOT `UTF8.GetBytes(PeerId.Value)`); update `tenantId` in HEARTBEAT payload to length-prefixed UTF-8; use `BinaryPrimitives.WriteUInt32BigEndian` / `WriteInt64BigEndian` per §A4
- [ ] Add ≥4 known-answer tests (existing round-trip tests verify self-consistency only; byte-level tests are required for wire-protocol correctness):
  1. **HELLO signable bytes** — construct a HELLO with fixed inputs (fixed ephemeral key, identity key, tenant "test-tenant", caps=0x03); assert the Ed25519 signing input is the exact expected byte sequence
  2. **HEARTBEAT signable bytes** — construct a HEARTBEAT with fixed inputs (fixed peerId raw bytes, tenant "test-tenant", caps=0x01, timestamp=1234567890); assert the Ed25519 signing input is the exact expected byte sequence
  3. **CONFIRM transcript hash** — construct a CONFIRM input with fixed HELLO_A, HELLO_B, ACCEPT, and caps; assert SHA-256 output equals expected hex
  4. **TenantId edge cases** — test `TenantId("")` (zero-length → uint32BE(0) with no following bytes), `TenantId("a")` (1 byte), and a 63-byte value; assert length-prefix byte sequences are correct
- [ ] Archive `cob-question-2026-05-04T21-22Z-w45-p2-adr-0076-a1.md` to research-inbox `_archive/` in this PR

**Estimated effort:** ~2-3h sunfish-PM (3 encoding sites + 4 known-answer tests; the test fixtures require constructing exact byte arrays by hand, which is the non-trivial portion).

---

### A1.8 References

- COB question: `icm/_state/research-inbox/cob-question-2026-05-04T21-22Z-w45-p2-adr-0076-a1.md`
- ADR 0076 base: §Wire protocol, §Handshake steps, §HEARTBEAT authentication
- `packages/federation-common/PeerId.cs` — `PeerId.From(PrincipalId)` definition
- `packages/foundation/` — `TenantId(string Value)` type (string-backed)

---

## Amendment A2 — Capability-negotiation verification: INVITE.capabilities in CONFIRM transcript

**Amendment date:** 2026-05-05
**Authors:** XO research session
**Resolves:** W#45 P4 council finding #8 (capability-downgrade MITM not detected)

---

### A2.1 Context

W#45 P4 pre-merge council returned finding #8: the A1 CONFIRM transcript hash bound
`negotiatedCap[1]` (ACCEPT.capability) and `presenceCapsA/B[1]` (HELLO.presence.caps) but did
NOT include `INVITE.capabilities[1]` (the offered capability set from the INVITE frame). A
relay-MitM can silently replace `INVITE.capabilities: 0x07` (text+audio+video) with `0x01`
(text-only); both sides then compute `ACCEPT.capability = 0x01` and both compute an identical
(but downgraded) transcript. Neither side detects the downgrade.

The A1 §Rationale stated: *"INVITE has only `capabilities: uint8` which is subsumed by
`negotiatedCap`."* This claim was incorrect. `INVITE.capabilities` is the *offered* bitmask; 
`ACCEPT.capability` is the *negotiated result*. They are distinct: an attacker changing the 
offer does not change the acceptance in a way either side can detect from the transcript alone.
A2 supersedes that portion of the A1 rationale and corrects the transcript spec.

---

### A2.2 Decision drivers

1. **Protocol completeness.** The CONFIRM transcript must commit all negotiation inputs, not 
   just the output. `INVITE.capabilities` is an input to the negotiation; `ACCEPT.capability` 
   is the output. Binding only the output allows the input to be manipulated silently.

2. **Consistency with A1.** A1 added `presence.caps` bindings to CONFIRM to close an analogous 
   relay-MitM downgrade vector on HELLO capabilities. The same principle applies to INVITE 
   capabilities; omitting it creates an asymmetry that the cohort's security discipline does 
   not tolerate.

3. **Minimal change.** One additional byte in the transcript hash; no new frame fields; no new 
   types; no breaking API change. The known-answer test for CONFIRM transcript (A1.7 test #3) 
   requires one additional fixture byte.

4. **Belt-and-suspenders initiator verification.** Adding `inviteCaps` to the transcript is 
   the primary fix. An additional check — initiator verifies `ACCEPT.capability ⊆ 
   INVITE.capabilities` before computing CONFIRM — provides defense-in-depth and is 
   trivially cheap.

---

### A2.3 Decisions

#### A1 ext — CONFIRM transcript hash extension (extends §A1.3 §A1)

**Replaces** the §A1.3 §A1 CONFIRM transcript hash spec, §A2.4 wire protocol CONFIRM row, 
and §A2.5 handshake step 9.

The CONFIRM `{ transcriptHash: bytes[32] }` MUST be computed as:

```
transcriptHash = SHA-256(
    ephemA[32]                                     // HELLO_A.ephemeralPublicKey
    || idA[32]                                     // HELLO_A.identityPublicKey
    || ephemB[32]                                  // HELLO_B.ephemeralPublicKey
    || idB[32]                                     // HELLO_B.identityPublicKey
    || uint32BE(len(tenantBytes)) || tenantBytes   // TenantId.Value as length-prefixed UTF-8
    || inviteCaps[1]                               // INVITE.capabilities (uint8) — A2 addition
    || negotiatedCap[1]                            // ACCEPT.capability (uint8)
    || presenceCapsA[1]                            // HELLO_A.presence.caps (uint8)
    || presenceCapsB[1]                            // HELLO_B.presence.caps (uint8)
)
```

Where (additions beyond §A1.3 §A1):
- `inviteCaps` = the `capabilities: uint8` byte from the INVITE frame.
  - **Initiator** uses the capabilities byte it put in its own INVITE frame.
  - **Responder** uses the capabilities byte it received in the INVITE frame.
  - If a relay-MitM has modified INVITE.capabilities in transit, the initiator and responder 
    will have different `inviteCaps` values → transcripts mismatch → both send REJECT → attack 
    detected.

**Total input size:** 32 + 32 + 32 + 32 + 4 + len(tenantBytes) + 1 + 1 + 1 + 1 = 136 + 
len(tenantBytes) bytes (previously 135 bytes per §A1.3).

**Correction of A1 §Rationale:** The §A1.3 §A1 rationale stated "INVITE has only 
`capabilities: uint8` which is subsumed by `negotiatedCap`." A2 demonstrates this was wrong: 
`INVITE.capabilities` is the offered set and `ACCEPT.capability` is the result. An attacker 
changing the offer leaves the acceptance result plausible from both sides' perspectives, 
producing identical (but wrong) transcripts. A2 corrects this.

---

#### A2 — Initiator post-ACCEPT verification

**New requirement** (between step 7 and step 9 of the handshake).

After receiving ACCEPT and before computing CONFIRM, the initiator MUST verify:

```
(ACCEPT.capability & INVITE.capabilities) == ACCEPT.capability
```

That is, `ACCEPT.capability` must be exactly one bit that is a member of the set the initiator 
offered in INVITE. If this check fails, the initiator MUST send REJECT and close.

**Why this is belt-and-suspenders:** if the relay-MitM downgraded INVITE.capabilities but left 
ACCEPT.capability consistent with the downgraded offer, the transcript mismatch catches it. The 
initiator verification catches the degenerate case where an attacker upgrades 
`ACCEPT.capability` beyond what was offered (which would NOT cause a transcript mismatch, since 
the initiator's `inviteCaps` is its own original offer). Together, the two checks close all 
capability-negotiation downgrade and upgrade vectors.

---

### A2.4 Updated wire protocol table (A2-ratified rows)

Replaces §A1.4 CONFIRM table row (which replaced the base ADR CONFIRM row):

| Byte | Name | Payload (ratified) |
|---|---|---|
| `0x0A` | `CONFIRM` | `{ transcriptHash: bytes[32] }` — SHA-256 over (ephemA[32] \|\| idA[32] \|\| ephemB[32] \|\| idB[32] \|\| uint32BE(len(tenantBytes)) \|\| tenantBytes \|\| inviteCaps[1] \|\| negotiatedCap[1] \|\| presenceCapsA[1] \|\| presenceCapsB[1]); both sides MUST verify agreement before entering ACTIVE |

---

### A2.5 Updated handshake step

Replaces §A1.5 step 9 (which replaced base ADR §Handshake step 9).

**Step 9 (CONFIRM computation):**
```
9. After ACCEPT, both sides independently compute:
   tenantBytes    = UTF8.GetBytes(localTenantId.Value)
   transcriptHash = SHA-256(
       HELLO_A.ephemeralPublicKey[32]
       || HELLO_A.identityPublicKey[32]
       || HELLO_B.ephemeralPublicKey[32]
       || HELLO_B.identityPublicKey[32]
       || uint32BE(len(tenantBytes)) || tenantBytes             // A1-ratified
       || INVITE.capabilities[1]                               // A2 addition — offered cap bitmask
       || ACCEPT.capability[1]                                  // A1-ratified: negotiatedCap only
       || HELLO_A.presence.caps[1]                             // A1 Q1 — caps bound to detect downgrade
       || HELLO_B.presence.caps[1]                             // A1 Q1 — caps bound to detect downgrade
   )
   and send CONFIRM { transcriptHash }.
   Mismatch → REJECT + close. Session enters ACTIVE only after both CONFIRMs verified.
```

**Additional initiator step (between step 7 and step 9):**
```
7a. (Initiator only) After receiving ACCEPT, before computing CONFIRM:
    VERIFY: (ACCEPT.capability & sentInviteCapabilities) == ACCEPT.capability
    If check fails → send REJECT + close. Do not proceed to step 8/9.
```

---

### A2.6 §A0 self-audit (per ADR 0069 discipline)

This amendment introduces **no new `Sunfish.*` types**. All changes are to the CONFIRM 
transcript hash specification and a new initiator verification step.

| Symbol / Path | Classification | Verified |
|---|---|---|
| `ChannelCapability` (uint8 bitmask) | Existing | yes — W#45 P1 `ChannelCapability` enum in `foundation-channels`; `INVITE.capabilities` is a flags-combined uint8 per ADR 0076 §Wire protocol `0x03` row |
| `INVITE.capabilities` field | Existing | yes — ADR 0076 §Wire protocol row `0x03`: `{ capabilities: uint8 }` |
| `ACCEPT.capability` field | Existing | yes — ADR 0076 §Wire protocol row `0x04`: `{ capability: uint8 }` |

No §A0.1 negative-existence check needed (no new types introduced).
§A0.2: `inviteCaps` is an additional byte in the hash input — no false-positive type risk.
§A0.3: The verification check `(ACCEPT.capability & INVITE.capabilities) == ACCEPT.capability` 
is bitwise AND — standard .NET pattern. Implementers MUST cast `ChannelCapability` enum values to 
`byte` at the wire boundary: `(byte)caps` when writing the frame field; `(ChannelCapability)b` when 
reading. Mixing enum and uint8 without explicit cast produces a compile-time error in C#; this is 
desirable and intentional.

---

### A2.7 Implementation checklist (W#45 P4 unblock — addendum to §A1.7)

- [ ] `EncryptionHandshake.ComputeConfirmHash()` — add `inviteCaps[1]` after tenantBytes 
  and before negotiatedCap; initiator passes its sent `INVITE.capabilities` byte; 
  responder passes the received `INVITE.capabilities` byte
- [ ] `SessionInitiator.OpenAsync()` — after receiving ACCEPT (step 7), add verification: 
  `if ((accepted.Capability & sentCapabilities) != accepted.Capability) → send REJECT + throw`
- [ ] Update §A1.7 known-answer test #3 (CONFIRM transcript hash) to include `inviteCaps[1]` 
  in the fixture byte sequence and expected SHA-256 hash

**Estimated effort:** ~45min sunfish-PM (one additional byte in one hash site + one equality 
check + one test fixture update). May be combined with the P4 AEAD + glare-wiring work.

---

### A2.8 References

- W#45 P4 COB question: `icm/_state/research-inbox/cob-question-2026-05-05T09-15Z-w45-p4-council-deferral-plan.md`
- W#45 P4 council review: `icm/07_review/output/adr-audits/0076-a2-council-review-2026-05-05.md` (finding #8 origin)
- ADR 0076-A1 §A1.3 §A1 (prior CONFIRM transcript hash spec — superseded by A2.3)
- ADR 0076 §Wire protocol `0x03` INVITE row
- `packages/foundation-channels/` — `ChannelCapability` flags enum (W#45 P1)

---

## Amendment A3 — Conformance test vectors (HELLO / HEARTBEAT / CONFIRM transcript)

**Amendment date:** 2026-05-05
**Authors:** XO research session
**Resolves:** A1 council review finding F3 (Major) — *"no test vectors for transcript-hash or HEARTBEAT signable; interop unfalsifiable"*
**Pipeline variant:** `sunfish-feature-change` (specification artifact only; no new types; no wire-encoding change)
**Pre-merge council:** mandatory (behavior-bearing artifact under cohort security discipline)

---

### A3.1 Context

A1 ratified the wire-encoding canonical form (transcript-hash, TenantId, PeerId encodings, endianness convention); A2 closed the relay-MitM capability-downgrade vector by binding `INVITE.capabilities` into the CONFIRM transcript hash. Both amendments specify the encoding **in prose only**: byte-layout descriptions, length tables, ordering rules. The A1 council review (`0076-A1-council-review-2026-05-04.md` finding F3) flagged this as a Major gap:

> The amendment ships pure prose; there are zero authoritative test vectors. The motivating concern for the amendment ("MessagePack key ordering is implementation-defined, meaning two conformant implementations may serialize identical logical payloads to different byte sequences") is exactly the kind of cross-implementation interop concern that test vectors are designed to lock down.

The cohort precedent for crypto specifications (RFC 8439 ChaCha20-Poly1305 §Test Vectors; RFC 7748 X25519 §Test Vectors; RFC 5869 HKDF §Test Vectors; ADR 0028 JSON canonical-form examples) is to ship known-answer fixtures alongside the prose. Without them, the gate "implementations agree on transcript-hash bytes" (council AP-18 finding) is unverifiable cross-implementation.

A3 closes F3 by defining a fixed, deterministic, version-controlled set of conformance test vectors that any implementation in any language MUST reproduce byte-for-byte to be considered conforming.

---

### A3.2 Decision drivers

1. **Interop falsifiability.** A second-language implementation (iOS Swift, Android Kotlin, Rust) cannot demonstrate conformance against prose alone. Test vectors fail loud and immediate when an implementer mis-implements length-prefix endianness, `peerId` encoding, or capability-byte position.

2. **Reproducibility from source.** Vectors are derived deterministically from short canonical phrases via SHA-256, not random seeds or binary blob fixtures. The entire vector set can be regenerated from the source script, so review/audit/regen does not require trusting committed binary data.

3. **Single canonical generator.** One authoritative tool (`tools/icm/generate-channel-vectors.py`) produces the vectors; all implementations verify against the JSON output. The .NET reference impl is NOT the canonical source — that would couple the spec to NSec.Cryptography quirks. Python's `cryptography` package implements RFC 8032 deterministic Ed25519 + RFC 7748 X25519 + FIPS 180-4 SHA-256, which any conforming implementation matches by construction.

4. **Cohort idiom — appendix-section over sidecar file.** A1 and A2 are inline amendments in the ADR file. A3 follows the same pattern; the JSON artifact is a separate file (`tools/icm/channel-test-vectors.json`) but the canonical specification of vector inputs + expected outputs lives in the ADR appendix below. No new ADR-sidecar precedent is set.

5. **Domain-separator (F2) deferred.** A1 council finding F2 (no domain-separator prefix on transcript-hash) is NOT addressed by A3. F2 is a forward-compatibility concern (allows reusing the same hash construction for a future protocol version without collision); F3 is the cross-implementation interop concern. Closing F3 first is correct because F3 blocks every second-language implementer immediately, while F2 only matters when a v2 protocol is designed. F2 will be a follow-on amendment when Phase 3 (audio) lands or when a second-language implementation is in flight. The A3 vectors as authored here will need ONE regeneration when F2 lands; this cost is accepted.

6. **No impl-vs-spec drift remediation in this amendment.** The .NET reference impl (`packages/blocks-crew-comms/Crypto/EncryptionHandshake.cs` on `main` as of W#45 P4) uses the A1-pre-ratification 6-parameter `ComputeTranscriptHash` signature (no `presence.caps` from A1, no `inviteCaps` from A2). The W#45 P4.5 hand-off (`icm/_state/handoffs/crew-comms-p45-stage06-addendum.md`) tracks the .NET impl catch-up as a separate sunfish-PM PR with its own pre-merge council. A3 vectors are the authoritative reference that the catch-up PR validates against; A3 does NOT modify the impl.

---

### A3.3 Decisions

#### A3 Decision 1 — Conformance test-vector format and storage

A3 authors a single canonical JSON document at:

- `tools/icm/channel-test-vectors.json` — committed to the repo; CI-verifiable

The document is generated by:

- `tools/icm/generate-channel-vectors.py` — Python 3 + `cryptography` (PyPI); deterministic; byte-stable; supports `--check` for CI drift detection

The schema is:

```jsonc
{
  "schema_version": 1,
  "adr_reference": "0076-A3",
  "domain_separator": null,
  "ratified_form": "A1+A2 (no domain-separator; F2 deferred to a later amendment)",
  "fixed_inputs": {
    "initiator_identity_seed_hex":   "<32-byte hex>",   // SHA-256 of canonical phrase
    "initiator_identity_pubkey_hex": "<32-byte hex>",   // Ed25519 raw public key
    "responder_identity_seed_hex":   "<32-byte hex>",
    "responder_identity_pubkey_hex": "<32-byte hex>",
    "initiator_x25519_seed_hex":     "<32-byte hex>",
    "initiator_x25519_pubkey_hex":   "<32-byte hex>",   // X25519 raw public key
    "responder_x25519_seed_hex":     "<32-byte hex>",
    "responder_x25519_pubkey_hex":   "<32-byte hex>",
    "sample_message_id":             "00000000-0000-4000-8000-000000000001",
    "sample_timestamp_unix_ms":      1735689600000      // 2025-01-01T00:00:00Z UTC
  },
  "seed_provenance": { /* documents how seeds are derived */ },
  "vectors": [
    {
      "id": "V1",
      "kind": "HELLO",        // or "HEARTBEAT" or "CONFIRM_TRANSCRIPT"
      "description": "...",
      "inputs":   { /* per-vector input bag (kind-specific) */ },
      "expected_signable_hex":   "...",   // HELLO + HEARTBEAT only
      "expected_signable_length": 84,     // HELLO + HEARTBEAT only
      "expected_signature_hex":  "...",   // HELLO + HEARTBEAT only
      "expected_input_hex":      "...",   // CONFIRM_TRANSCRIPT only
      "expected_input_length":   151,     // CONFIRM_TRANSCRIPT only
      "expected_sha256_hex":     "..."    // CONFIRM_TRANSCRIPT only
    }
    // ...
  ]
}
```

All hex values are lowercase, unpadded, no `0x` prefix.

#### A3 Decision 2 — Vector coverage

A3 ships **9 vectors**: 3 HELLO, 3 HEARTBEAT, 3 CONFIRM-transcript-hash. Coverage rationale:

| Kind | V# | Coverage axis | Tenant | Caps inputs |
|---|---|---|---|---|
| HELLO | V1 | Normal-shape ASCII tenant | `"tenant-001-acme"` (15 bytes) | presence.caps = 0x07 (text+audio+video) |
| HELLO | V2 | Zero-length tenant edge | `""` (0 bytes) | presence.caps = 0x01 (text only) |
| HELLO | V3 | UTF-8 multi-byte tenant | `"tenant-é-ünïcödë"` (21 bytes) | presence.caps = 0x03 (text+audio) |
| HEARTBEAT | V4 | Normal-shape ASCII tenant | `"tenant-001-acme"` (15 bytes) | caps = 0x01, ts = 1735689600000 |
| HEARTBEAT | V5 | Max-practical 63-byte ASCII tenant | `"x" * 63` | caps = 0x07, ts = 1735689600000 |
| HEARTBEAT | V6 | Boundary 1-byte tenant | `"a"` (1 byte) | caps = 0x01, ts = 1735689600000 |
| CONFIRM | V7 | Full A1+A2 form, normal tenant | `"tenant-001-acme"` | inviteCaps=0x07, negCap=0x01, presA=0x07, presB=0x03 |
| CONFIRM | V8 | Zero-length tenant + minimal caps | `""` | All four caps fields = 0x01 |
| CONFIRM | V9 | UTF-8 tenant + asymmetric caps | `"tenant-é-ünïcödë"` | inviteCaps=0x07, negCap=0x02, presA=0x07, presB=0x06 |

This covers the three failure modes the council called out:
- **Length-prefix endianness** (caught by V5's 0x3F = 63 length byte vs little-endian's 0x3F00 mis-encoding)
- **Tenant boundary** (V2 zero-length and V6 one-byte expose off-by-one length-prefix bugs)
- **UTF-8 byte-vs-character** (V3 + V9 fail on implementations that encode `tenant.length` as character count rather than byte count)

The CONFIRM vectors additionally lock the **field ordering** (A1 step `presenceCapsA[1]` after `negotiatedCap[1]`; A2 insertion of `inviteCaps[1]` between `tenantBytes` and `negotiatedCap`) — the exact ordering the council flagged as silently mis-implementable.

#### A3 Decision 3 — Test seeds (deterministic, reproducible)

| Role | Algorithm | Seed = SHA-256 of phrase |
|---|---|---|
| Initiator identity | Ed25519 | `"sunfish-channels-test-initiator-id-v1"` |
| Responder identity | Ed25519 | `"sunfish-channels-test-responder-id-v1"` |
| Initiator ephemeral | X25519 | `"sunfish-channels-test-initiator-ephem-v1"` |
| Responder ephemeral | X25519 | `"sunfish-channels-test-responder-ephem-v1"` |

Resolved keys (informative — see `tools/icm/channel-test-vectors.json` `fixed_inputs` for the canonical artifact):

```
initiator identity pubkey (Ed25519, 32B):  4dba7077e2cbb3f4b66e1fb8e07911c9110d918326e707f60b8494974e85db35
responder identity pubkey (Ed25519, 32B):  01f61a817230f3abf2e2b88665cbd05d44aa6d8dfcdc6a58cfa24df20b6cd50a
initiator ephemeral pubkey (X25519, 32B):  005f111c8869fa005c1df5c8c775eb95a6a7dca4393e5df3ad152e017d78b23e
responder ephemeral pubkey (X25519, 32B):  0238bb7243d92826e653ae2b9f98b2fe93661fe19e5e53ca40d8f1552389fb3c
```

> **Test-only fixtures — DO NOT use in production.** These keys are
> deterministic and publicly committed in this repository. They MUST
> NEVER be installed in any production roster, used to sign any
> production frame, or treated as confidential. Any peer that produces
> a HELLO or HEARTBEAT signature with these identity keys is by
> construction non-production-bound.

#### A3 Decision 4 — Expected outputs (informative summary; canonical artifact in JSON)

The full byte-level expected outputs live in `tools/icm/channel-test-vectors.json`. The summary table below pins the SHA-256 transcript-hash outputs and signable lengths so reviewers can spot-check at a glance:

| Vector | Kind | Signable / input length | Expected output (truncated for table) |
|---|---|---|---|
| V1 | HELLO | 84 B | sig = `b185c034c93a312a670fb46ae30b4818…` |
| V2 | HELLO | 69 B | sig = `5aabffa12fdee040cf05cd001124fa54…` |
| V3 | HELLO | 90 B | sig = `ca41dafb02e5be88993b32a73352d3f7…` |
| V4 | HEARTBEAT | 60 B | sig = `fe31f6e8c1641b585a87ff8f52d7aa8a…` |
| V5 | HEARTBEAT | 108 B | sig = `659265c63a6112faf1b9f19fbb79a49a…` |
| V6 | HEARTBEAT | 46 B | sig = `13736db960d440d2df49c7ebd926dc37…` |
| V7 | CONFIRM | 151 B | SHA-256 = `5c38292a921ea0bff9a3b20b49e255d8e8eb06579e1aaa44aa11ad539f03a8fb` |
| V8 | CONFIRM | 136 B | SHA-256 = `de32c848e5a0c63e7919201a2d984a2d7fc0efef0ba928a4a3845ba02ee4d039` |
| V9 | CONFIRM | 157 B | SHA-256 = `852c278135eccb596882767608598806eeefca4c5b546e22984497102d906cb6` |

**Length-math validation** (per ADR §A1.4, §A2.3 §A1 ext):
- HELLO: `32 + 32 + 4 + len(tenantBytes) + 1` → V1 = 32+32+4+15+1 = **84 ✓**, V2 = 32+32+4+0+1 = **69 ✓**, V3 = 32+32+4+21+1 = **90 ✓**
- HEARTBEAT: `32 + 4 + len(tenantBytes) + 1 + 8` → V4 = 32+4+15+1+8 = **60 ✓**, V5 = 32+4+63+1+8 = **108 ✓**, V6 = 32+4+1+1+8 = **46 ✓**
- CONFIRM (A1+A2): `4*32 + 4 + len(tenantBytes) + 4*1` → V7 = 128+4+15+4 = **151 ✓**, V8 = 128+4+0+4 = **136 ✓**, V9 = 128+4+21+4 = **157 ✓**

---

### A3.4 Cross-implementation conformance protocol

A conforming implementation in any language (.NET, Swift, Kotlin, Rust, Go, …) MUST satisfy all three checks below. A failure on any vector means the implementation is non-conforming and cannot interop with peers that follow the spec.

**Check 1 — Signable byte assembly.** For each `HELLO` and `HEARTBEAT` vector, the implementation reconstructs the signable buffer from `inputs` and asserts byte-for-byte equality against `expected_signable_hex`. This catches length-prefix endianness, byte vs character length, field-ordering, and `peerId` encoding bugs. (A signature mismatch alone does not localize the defect; the signable check does.)

**Check 2 — Ed25519 signature.** For each `HELLO` and `HEARTBEAT` vector, the implementation signs the `expected_signable_hex` with the test seed (`initiator_identity_seed_hex` or `responder_identity_seed_hex`) using its Ed25519 implementation, and asserts byte-for-byte equality against `expected_signature_hex`. Per RFC 8032 Ed25519 is fully deterministic — there is no nonce/IV randomness — so any conforming implementation produces identical signature bytes.

**Check 3 — CONFIRM SHA-256 transcript hash.** For each `CONFIRM_TRANSCRIPT` vector, the implementation reconstructs the input buffer from `inputs` and asserts byte-for-byte equality against `expected_input_hex`, then computes SHA-256 and asserts byte-for-byte equality against `expected_sha256_hex`. Both checks are required: input-buffer match without hash match would indicate SHA-256 implementation drift (very unlikely); hash match without input-buffer match would indicate a coincidentally-colliding bug (vanishingly unlikely but mathematically possible) — the input check is what makes the failure mode legible.

**Verification cadence:** the conformance suite is a build-time gate, not a runtime gate. It runs in CI on every PR that touches `EncryptionHandshake`, `FrameProtocol`, `Payloads`, or any cross-language re-implementation of the wire encoding. A failing vector blocks merge.

**Per-language fixture loader:** the JSON document is the source of truth. Each language implements one loader (e.g. `EncryptionHandshakeConformanceTests` in xUnit for .NET; an equivalent in `XCTest` for Swift; in `kotest` for Kotlin) that reads the JSON, drives Checks 1–3, and reports per-vector pass/fail. The loader is implementation-private; the JSON is shared.

---

### A3.5 Generation provenance

The canonical generator is `tools/icm/generate-channel-vectors.py`. It uses:

| Primitive | Source | Notes |
|---|---|---|
| Ed25519 sign / public-key derivation | Python `cryptography` package (via PyCA, libssl) | RFC 8032 deterministic; no nonce randomness |
| X25519 public-key derivation | Python `cryptography` package | RFC 7748; no actual key-agreement is performed (only public-key derivation from seed) — A3 vectors do not contain DH-derived shared secrets, so HKDF / session-key correctness is NOT covered by these vectors and remains an integration-test concern |
| SHA-256 | Python stdlib `hashlib` | FIPS 180-4 |

**Why not the .NET reference impl as the canonical generator?** Coupling the spec to a single library's quirks (NSec.Cryptography wraps libsodium) creates a hidden dependency: a future second-language implementation that uses a different Ed25519 backend (e.g. swift-crypto, ring, BoringSSL) cannot validate against the spec without trusting that the .NET impl matched RFC 8032 exactly. By using a Python reference path (PyCA `cryptography` is a thin wrapper over OpenSSL/libssl Ed25519, also RFC 8032), the spec becomes library-portable: any RFC-conforming Ed25519 produces identical signatures.

**Determinism rule.** `tools/icm/generate-channel-vectors.py --check` MUST exit 0 when run against the committed JSON. CI enforces this. Re-running the script must produce byte-identical JSON output (verified at authoring time: two consecutive runs produced an unchanged 11,980-byte document).

**Update path.** When (not if) F2 lands and a domain-separator prefix is added to the transcript-hash construction, V7/V8/V9 expected hashes change. The amendment that introduces F2 will:
1. Update `confirm_transcript_input` in the generator script to prepend the domain-separator
2. Re-run `python3 tools/icm/generate-channel-vectors.py`
3. Commit both the generator change and the regenerated `channel-test-vectors.json`
4. Update the §A3.3 D4 SHA-256 column above with the new hashes

The HELLO and HEARTBEAT vectors are unaffected by F2 (the domain-separator concern is CONFIRM-transcript-only).

---

### A3.6 §A0 self-audit (per ADR 0069 D1 discipline)

This amendment introduces **no new `Sunfish.*` types**. Only specification artifacts (one Python tool + one JSON document + one ADR appendix).

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Crypto.PrincipalId.AsSpan()` | Existing | yes — `packages/foundation/Crypto/PrincipalId.cs` line 49: `public ReadOnlySpan<byte> AsSpan() => _bytes;` returns 32 raw Ed25519 pubkey bytes; A3 §A3.4 Check 1 references this for HEARTBEAT signable |
| `Sunfish.Foundation.Crypto.KeyPair.Sign(ReadOnlySpan<byte>)` | Existing | yes — `packages/foundation/Crypto/KeyPair.cs` line 54: `public Signature Sign(ReadOnlySpan<byte> data)` returns NSec `Signature`; A3 §A3.4 Check 2 verifies cross-implementation matches |
| `Sunfish.Foundation.Crypto.KeyPair.VerifyRaw(...)` | Existing | yes — `KeyPair.cs` line 67: `public static bool VerifyRaw(...)`; cited in `EncryptionHandshake.VerifyHelloAsync` and `EncryptionHandshake.VerifyHeartbeat` |
| `Sunfish.Federation.Common.PeerId.From(PrincipalId)` | Existing | yes — `packages/federation-common/PeerId.cs`; `PeerId` is base64url; A3 §A3.4 Check 1 explicitly mandates `PrincipalId.AsSpan()` (raw 32B), NOT `Encoding.UTF8.GetBytes(PeerId.Value)` (43B base64url) — preserves A1 §A1.3 §A3 mandate |
| `Sunfish.Foundation.Assets.Common.TenantId(string Value)` | Existing | yes — string-backed; A3 vector inputs use `tenant_id_value` field (string) and derive `tenant_bytes_hex` as `UTF8.GetBytes(value)` consistently with A1 §A1.3 §A2 |
| `Sunfish.Foundation.Channels.ChannelCapability` (uint8 flags) | Existing | yes — W#45 P1 `foundation-channels`; A3 vectors use raw byte values (0x01 text, 0x02 audio, 0x04 video, 0x07 all) consistent with the flags-enum cast contract |
| `tools/icm/generate-channel-vectors.py` | New (authored in A3) | yes — committed in this PR; cohort precedent `tools/icm/render-ledger.py` for the byte-stable `--check` pattern |
| `tools/icm/channel-test-vectors.json` | New (authored in A3) | yes — committed in this PR |

**§A0.1 (negative existence — no parallel-session pre-emption):** `gh pr list --search "ADR 0076"` (run 2026-05-05) shows no in-flight A3 PR; PR #566 already merged a different A2 amendment (capability-negotiation verification), confirming this F3-resolution amendment is correctly numbered A3. No collision.

**§A0.2 (no false-positive citations):** Every cited Sunfish.* symbol above was opened and read on `origin/main` (commit `7da6804`). The `EncryptionHandshake.cs` impl on `main` uses a 6-parameter `ComputeTranscriptHash` signature; A3 vectors reflect the **specified** (A1+A2-ratified, 9-equivalent-input) form, not the impl form. This impl-vs-spec drift is acknowledged here, tracked separately by the W#45 P4.5 hand-off (`crew-comms-p45-stage06-addendum.md` PR 1), and remediated by a future sunfish-PM PR that the A3 vectors will validate against. A3 does NOT modify the impl.

**§A0.3 (structural correctness of every cited byte layout):**
- `uint32BE` length-prefix encoding verified against §A1.3 §A2 explicit text and §A4 endianness convention
- HEARTBEAT signable order `peerId_raw[32] || uint32BE(len) || tenantBytes || caps[1] || timestamp_BE[8]` verified against §A1.3 §A3 final paragraph
- CONFIRM transcript order `ephemA || idA || ephemB || idB || u32be(len) || tenantBytes || inviteCaps || negotiatedCap || presenceCapsA || presenceCapsB` verified against §A2.5 step 9 (the A2-superseding form, NOT the A1.5 form)
- Length-math (84/69/90/60/108/46/151/136/157) cross-checked against §A1.3 §A1 "Total input size: 32+32+32+32+4+len(tenantBytes)+1+1+1 = 135+len" formula updated by §A2.3 §A1ext to "+1 = 136+len(tenantBytes)" — which V7 (151 = 136+15) and V8 (136 = 136+0) and V9 (157 = 136+21) confirm.

---

### A3.7 Implementation checklist (information-only — no impl change in this PR)

A3 itself ships only the spec + Python generator + JSON artifact. The downstream consumers are tracked separately:

- [ ] (W#45 P4.5 PR 1; sunfish-PM) Update `EncryptionHandshake.ComputeTranscriptHash` to the 9-parameter A1+A2 signature, add the four caps inputs, and verify against vectors V7/V8/V9 in a known-answer xUnit test
- [ ] (W#45 P4.5 PR 1; sunfish-PM) Add HELLO/HEARTBEAT known-answer xUnit tests against vectors V1–V6 (loads `tools/icm/channel-test-vectors.json` via path; asserts signable + signature byte-for-byte)
- [ ] (W#45 P4.5 PR 1; sunfish-PM) Update §A1.7 / §A2.7 implementation checklist items to cite A3 vectors as the canonical reference
- [ ] (Future second-language impl, e.g. iOS Swift for W#23) Implement an XCTest fixture loader that drives Checks 1–3 against the same JSON
- [ ] (Future amendment closing F2) Add domain-separator prefix to `confirm_transcript_input`; regenerate vectors; replace V7/V8/V9 expected outputs in §A3.3 D4 + JSON

**Estimated effort for the W#45 P4.5 PR 1 conformance-test addition:** ~1.5h on top of the existing P4.5 transcript-hash alignment work — one JSON loader + 9 known-answer assertions. Pre-merge council mandatory (per P4.5 hand-off; security artifact).

**A3 is NOT a build-or-implementation amendment.** No COB action is required to ship A3 itself; its consumption happens in the W#45 P4.5 PR 1 cycle.

---

### A3.8 References

- A1 council review (F3 origin): `icm/07_review/output/adr-audits/0076-A1-council-review-2026-05-04.md`
- A1 ratification: `docs/adrs/0076-crew-comms-foundation-channels.md` §Amendment A1 (PR #564)
- A2 capability-negotiation: §Amendment A2 (PR #566)
- W#45 P4.5 hand-off (downstream consumer): `icm/_state/handoffs/crew-comms-p45-stage06-addendum.md`
- Cohort precedent for byte-stable Python generation tools: `tools/icm/render-ledger.py`
- Cohort precedent for crypto known-answer tests: `packages/foundation-recovery/tests/PaperKeyDerivationTests.cs` (BIP-39 vectors)
- IETF crypto-spec test-vector convention: RFC 8439 §2.1.1 (ChaCha20-Poly1305); RFC 7748 §6.1 (X25519); RFC 5869 §A (HKDF); RFC 8032 §7 (Ed25519)
- Generator: `tools/icm/generate-channel-vectors.py`
- Canonical artifact: `tools/icm/channel-test-vectors.json`
