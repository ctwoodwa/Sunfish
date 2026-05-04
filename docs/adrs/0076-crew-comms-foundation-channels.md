---
id: "0076"
title: Crew Comms — foundation-channels Contracts and Native Implementation
status: Accepted
date: 2026-05-04
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - communication
  - transport
  - security
  - presence

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
  - date: 2026-05-04
    summary: >
      A1 wire-encoding ratification (unblocks W#45 P2): (1) transcript-hash
      canonical form changed to reduced field-extract
      SHA-256(ephemA||idA||ephemB||idB||uint32BE(len(tenantBytes))||tenantBytes||negotiatedCap)
      — robust to MessagePack key-ordering; full-frame-bytes are brittle and implementation-matched
      reduced form; (2) tenantId wire encoding redefined as UTF-8 bytes of TenantId.Value (string-backed
      value object; no UUID semantics; avoids touching every multi-tenant package); (3) peerId wire
      encoding redefined as raw bytes[32] Ed25519 identity public key (PrincipalId bytes, base64url
      string-equivalent PeerId.From(PrincipalId)); UUID encoding mandate scoped to messageId fields only.
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
| `0x01` | `HELLO` | bidirectional on connect | `{ ephemeralPublicKey: bytes[32], identityPublicKey: bytes[32], tenantId: bytes (UTF-8 of TenantId.Value), signature: bytes[64], presence: PresenceHeartbeat }` — `signature` = Ed25519(longTermPrivKey, ephemeralPublicKey \|\| identityPublicKey \|\| UTF8(tenantId.Value)) |
| `0x02` | `HEARTBEAT` | broadcast | `{ peerId: bytes[32] (raw Ed25519 identity public key), tenantId: bytes (UTF-8 of TenantId.Value), caps: uint8, timestamp: int64, signature: bytes[64] }` — `signature` = Ed25519(longTermPrivKey, peerId[32] \|\| UTF8(tenantId.Value) \|\| caps[1] \|\| timestamp[8 BE]) |
| `0x03` | `INVITE` | initiator → recipient | `{ capabilities: uint8 }` — flags-combined; negotiation picks highest common capability |
| `0x04` | `ACCEPT` | recipient → initiator | `{ capability: uint8 }` — negotiated level |
| `0x05` | `REJECT` | recipient → initiator | `{ reason: string? }` |
| `0x0A` | `CONFIRM` | both sides after ACCEPT | `{ transcriptHash: bytes[32] }` — SHA-256(ephemA[32] \|\| idA[32] \|\| ephemB[32] \|\| idB[32] \|\| uint32BE(len(tenantBytes)) \|\| tenantBytes \|\| negotiatedCap[1]); both sides MUST verify agreement before entering ACTIVE. See §A1 rationale. |
| `0x06` | `BYE` | either direction | `{}` |
| `0x07` | `TYPING` | either in ACTIVE | `{}` — suppressed 3s after last keystroke |
| `0x08` | `DELIVERED` | either in ACTIVE | `{ messageId: bytes[16] }` — RFC 4122 big-endian UUID |
| `0x09` | `MUTE_STATE` | either in ACTIVE | `{ isMuted: bool }` — Phase 3 |
| `0x10` | `TEXT` | either in ACTIVE | `{ messageId: bytes[16], message: string }` — `messageId` RFC 4122 big-endian UUID |
| `0x20` | `AUDIO_FRAME` | either in ACTIVE | opaque Opus packet — Phase 3 |
| `0x30` | `VIDEO_FRAME` | either in ACTIVE | opaque H.264/VP8 — Phase 4 |

**UUID/GUID encoding in MessagePack payloads:** `messageId` fields (`DELIVERED 0x08`, `TEXT 0x10`) carry RFC 4122 UUIDs and MUST be encoded as `fixext 16` in RFC 4122 big-endian byte order. Do NOT use `Guid.ToByteArray()` — it produces a mixed-endian layout (little-endian `Data1`, little-endian `Data2`/`Data3`, big-endian `Data4`). Instead write each UUID component with `BinaryPrimitives.WriteUInt32BigEndian` / `WriteUInt16BigEndian`, or use a normalizing MessagePack extension that guarantees RFC 4122 byte order. Failure to normalize breaks interoperability with any non-.NET endpoint. **`tenantId` and `peerId` are not UUIDs** — they are raw byte sequences (see A1 amendment above); the `fixext 16` constraint does not apply to them.

**Encryption handshake (on every connection — no session resumption):**

```
1. Both peers generate ephemeral X25519 key pair
2. Construct HELLO: { ephemeralPublicKey, identityPublicKey (Ed25519), tenantId (UTF-8 bytes of TenantId.Value),
      signature = Ed25519Sign(longTermPrivKey, ephemeralPublicKey[32] || identityPublicKey[32] || UTF8(tenantId.Value)) }
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
   tenantBytes = UTF8(tenantId.Value)
   transcriptHash = SHA-256(
     ephemA[32] || idA[32] ||         // from HELLO_A
     ephemB[32] || idB[32] ||         // from HELLO_B
     uint32BE(len(tenantBytes)) || tenantBytes ||  // tenant binding
     negotiatedCap[1]                 // ACCEPT.capability byte
   )
   and send CONFIRM { transcriptHash }.
   Mismatch → REJECT + close. Session enters ACTIVE only after both CONFIRMs verified.
   Rationale (A1): reduced-form is robust to MessagePack key-ordering variance across
   platforms; full-frame-bytes bind to serialization details rather than semantic content.
```

**§A1 — Wire-encoding ratification (2026-05-04, unblocks W#45 P2):**
Three wire-encoding choices ratified after P2 council pre-merge review (1 Critical + 4 Major):

1. **Transcript-hash canonical form (Critical finding).** The original draft specified `SHA-256(HELLO_A_bytes || HELLO_B_bytes || INVITE_bytes || ACCEPT_bytes)` — binding to full MessagePack serialization of the framed messages. This is brittle: MessagePack key ordering is implementation-defined, meaning two conformant implementations may serialize identical logical payloads to different byte sequences, causing spurious CONFIRM mismatches across platforms. **Decision: reduced field-extract form** — `SHA-256(ephemA[32] || idA[32] || ephemB[32] || idB[32] || uint32BE(len(tenantBytes)) || tenantBytes || negotiatedCap[1])` — extracting fixed-width semantic fields directly. This form is invariant to MessagePack key ordering. The length-prefix on `tenantBytes` prevents extension attack via variable-length field adjacency. The implementation already matched the reduced form; the ADR text now matches the implementation.

2. **`tenantId` wire encoding.** The original draft specified `tenantId: uuid (fixext 16, RFC 4122)`. However, `Sunfish.Foundation.Assets.Common.TenantId` is `string Value`-backed with no UUID semantics — forcing UUID encoding would require redefining `TenantId` across every multi-tenant package. **Decision: `tenantId` encodes as raw UTF-8 bytes of `TenantId.Value`** in HELLO and HEARTBEAT. This is internally consistent with the rest of the multi-tenant surface and avoids a cross-package breaking change.

3. **`peerId` wire encoding.** The original draft specified `peerId: uuid (fixext 16, RFC 4122)`. The implementation's `PeerId.From(PrincipalId)` produces the raw 32-byte Ed25519 identity public key. **Decision: `peerId` encodes as raw `bytes[32]`** (the raw Ed25519 public key). This is semantically correct (the peer *is* identified by its public key) and consistent with `identityPublicKey: bytes[32]` already in HELLO. The `fixext 16` UUID constraint is now scoped to `messageId` fields only.

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
- **HEARTBEAT authentication:** each HEARTBEAT carries an Ed25519 signature over `(peerId[32] || UTF8(tenantId.Value) || caps[1] || timestamp[8 BE])` where `peerId` is the raw 32-byte Ed25519 identity public key. Receivers MUST verify before accepting presence updates; reject on failure. This prevents roster poisoning from unauthenticated broadcast frames.
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
- [ ] Implement `FrameProtocol` — length-prefix framing + MessagePack encode/decode for all v1 message types; RFC 4122 big-endian UUID encoding for `messageId` fields only (not `Guid.ToByteArray()`); `tenantId` as raw UTF-8 bytes; `peerId` as raw bytes[32] Ed25519 key (A1)
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
