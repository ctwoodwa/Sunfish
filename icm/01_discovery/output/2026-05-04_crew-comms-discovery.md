# Stage 01 Discovery — Crew Comms (W#45)

**Date:** 2026-05-04
**Intake:** `icm/00_intake/output/2026-05-04_crew-comms-intake.md`
**Scope:** `foundation-channels` contracts + `blocks-crew-comms` reference implementation

---

## §1 Foundation-Transport Dependency Mapping

The channel layer sits above `foundation-transport` and consumes exactly two entry points:

| Entry point | What it gives us | Used for |
|---|---|---|
| `ITransportSelector.SelectAsync(PeerId, ct)` → `IPeerTransport` | Best available transport tier for a given peer | Initiating a channel connection |
| `IPeerTransport.ConnectAsync(PeerId, ct)` → `IDuplexStream` | Connected duplex byte stream | Carrying channel frames |
| `MdnsPeerTransport._cache` (internal, via `ResolvePeerAsync`)| LAN-visible peers | Presence on local network |

`IDuplexStream` is a raw byte stream — no framing, no ordering guarantees beyond TCP. The channel layer must add framing on top.

**`PeerId`** (from `federation-common`) is the peer identity primitive — an Ed25519 public key encoded as a base64url string. This is the addressing key for everything in the channel layer.

**What the transport layer does NOT give us:**
- Peer presence / online status
- Capability advertisement (text / audio / video)
- Session signaling (invite / accept / reject)
- Message framing
- Encryption (Tier 1 TCP and Tier 3 relay are unencrypted at transport level)

All four gaps belong to `foundation-channels`.

---

## §2 Presence Protocol Decision

**The problem:** `ITransportSelector.SelectAsync` can connect to any `PeerId`, but it tells us nothing about whether a peer is online or what communication capabilities they support. We need a presence model to populate a "who's available to talk?" crew roster in the UI.

### Options considered

**Option A — mDNS TXT augmentation**
Extend `MdnsPeerTransportOptions` to carry capability TXT records (`caps=text,audio`) in the mDNS advertisement. Presence = being in the mDNS cache.
- Pro: zero round-trips; LAN presence is free.
- Con: LAN-only — doesn't work cross-network. Two mechanisms needed for the two tiers.

**Option B — Presence beacon on connect**
After the transport establishes a `IDuplexStream`, the first protocol exchange is a `HELLO` frame advertising capabilities. The caller learns presence at connection time.
- Pro: works across all tiers; single code path.
- Con: presence requires a full connection — can't show "online" before attempting to call.

**Option C — Push heartbeat bus**
Each node periodically broadcasts a heartbeat (with capability list) to all known crew peers. Peers maintain a local presence roster with TTL-eviction.
- Pro: rich presence UX (always see who's online); decoupled from sessions.
- Con: continuous background traffic; requires knowing the crew roster up front (tenant identity layer needed); adds background worker to Anchor.

**Option D — Tenant roster + on-demand probe**
The tenant identity layer (already knows crew members) provides the roster; the channel layer probes specific peers when the UI requests presence.
- Pro: no background traffic; roster from authoritative source.
- Con: probe latency on first view; presence stale until refreshed.

### Decision: Option C (push heartbeat) as the foundation; Option A as optimization

**Rationale:**

The compelling MVP demo is a live crew roster — "Chris is online, Sarah is online" — before any call is initiated. That requires Option C semantics. The heartbeat period can be generous (every 30s) so background traffic is negligible.

For LAN deployments, Option A (mDNS TXT) gives free near-zero-latency presence as a fast path; the heartbeat bus is the fallback and cross-network path. The two mechanisms are additive, not competing — mDNS presence populates the roster immediately on LAN; heartbeats keep it accurate and extend to relay-reachable peers.

**Presence TTL:** 90 seconds. A peer that misses 3 heartbeat windows is considered offline. This tolerates a single missed heartbeat without false-negative.

**Capability advertisement in heartbeat:**
```
PresenceHeartbeat {
    PeerId:    string          // sender's peer id
    TenantId:  string          // tenant scoping
    Caps:      uint8           // ChannelCapability flags bitmask
    DisplayName: string        // crew member display name
    IssuedAt:  int64           // Unix epoch seconds (UTC)
}
```
Framed as a channel protocol message (§4). Sent to all known crew peers via `ITransportSelector`.

---

## §3 Signaling Protocol — State Machine

Signaling runs on the same `IDuplexStream` as data, distinguished by message type. The state machine is per-session:

```
IDLE
  │
  │  (initiator calls OpenAsync)
  ▼
INVITING ──────────────────────────────── REJECT received → TERMINATED
  │
  │  ACCEPT received
  ▼
ACTIVE  ─── data frames (text/audio/video) ───────────────────────────
  │
  │  BYE sent or received
  ▼
TERMINATED
```

Peer B (recipient) state:

```
IDLE
  │
  │  INVITE received (surfaced via ListenAsync)
  ▼
INVITED
  │           │
  │ Accept()  │ Reject()
  ▼           ▼
ACTIVE     TERMINATED
  │
  │  BYE sent or received
  ▼
TERMINATED
```

**Timeout rule:** An unanswered INVITE transitions to TERMINATED after 60 seconds. The inviting side surfaces this as `ChannelSessionState.Terminated` with reason `InviteTimeout`.

**BYE semantics:** Either side can send BYE at any time. Receipt of BYE puts the session in TERMINATED immediately — no ACK needed. The sender waits 2 seconds for any in-flight data to drain before closing the `IDuplexStream`.

---

## §4 Message Framing Protocol

`IDuplexStream` is raw bytes. The channel protocol uses length-prefix framing:

```
┌─────────────────┬────────────┬─────────────────────────┐
│  Length (4B LE) │  Type (1B) │  Payload (Length-1 bytes)│
└─────────────────┴────────────┴─────────────────────────┘
```

The `Length` field covers `Type` + `Payload` (not itself). Max frame size: 64 KB for control frames; 256 KB for audio/video frames (Phase 3+).

**Message type registry (v1):**

| Byte | Name | Direction | Payload |
|---|---|---|---|
| `0x01` | `HELLO` | bidirectional, sent on connect | `PresenceHeartbeat` (see §2) |
| `0x02` | `HEARTBEAT` | broadcast, no open stream needed | `PresenceHeartbeat` |
| `0x03` | `INVITE` | initiator → recipient | `{ capabilities: uint8[] }` — priority-ordered; recipient accepts at highest mutually supported level |
| `0x04` | `ACCEPT` | recipient → initiator | `{ capability: uint8 }` — the negotiated level |
| `0x05` | `REJECT` | recipient → initiator | `{ reason: string? }` |
| `0x06` | `BYE` | either direction | `{}` |
| `0x07` | `TYPING` | either direction in ACTIVE | `{}` — sent on keystroke; suppressed for 3s after last key |
| `0x08` | `DELIVERED` | either direction in ACTIVE | `{ messageId: uuid }` — ack for a received TEXT frame |
| `0x09` | `MUTE_STATE` | either direction in ACTIVE | `{ isMuted: bool }` — Phase 3; sent on local mute toggle |
| `0x10` | `TEXT` | either direction in ACTIVE | `{ messageId: uuid, message: string (UTF-8) }` |
| `0x20` | `AUDIO_FRAME` | either direction in ACTIVE | `opaque bytes (Opus packet)` — Phase 3 |
| `0x30` | `VIDEO_FRAME` | either direction in ACTIVE | `opaque bytes (H.264/VP8)` — Phase 4 |

Payload encoding: MessagePack (compact, no schema drift, already used in federation-common). Fallback: raw UTF-8 for `TEXT` type if MessagePack is unavailable in a compat adapter scenario.

**Why MessagePack over JSON:** The framing carries audio/video frames in Phase 3+. MessagePack handles binary payloads natively; JSON would require base64 encoding adding ~33% overhead on every audio packet.

---

## §5 End-to-End Encryption

### The gap

Tier 1 (TCP over LAN) is plaintext. Tier 3 (Bridge relay) is ciphertext-only *at the relay* — but the relay doesn't encrypt, it forwards. Either way, the channel layer must encrypt.

### Options considered

**Option A — TLS over IDuplexStream**
Wrap `IDuplexStream.Stream` in `SslStream`. Standard, familiar, well-supported.
- Pro: battle-tested; handles forward secrecy.
- Con: heavyweight; requires certificate infrastructure (who issues certs to Anchor nodes?); awkward for peer-to-peer (not server-to-client).

**Option B — Noise Protocol (Noise_XX pattern)**
Purpose-built for peer-to-peer authenticated encryption. Uses `PeerId` (Ed25519) as the identity key. WireGuard uses Noise.
- Pro: designed exactly for this; forward secrecy; identity binding from existing Ed25519 keys; no cert infrastructure.
- Con: no built-in .NET implementation; needs a Noise library (e.g., `Noise.NET`) or manual implementation.

**Option C — HKDF + ChaCha20-Poly1305 over X25519 DH**
Manual but minimal: convert the two peers' Ed25519 keys to X25519 (standard `ed25519_to_curve25519` conversion), do ephemeral DH, use HKDF-SHA256 to derive a session key, use ChaCha20-Poly1305 (available in .NET 6+ via `System.Security.Cryptography.ChaCha20Poly1305`) for AEAD per-message.
- Pro: entirely in .NET BCL (`System.Security.Cryptography`); uses existing `PeerId` Ed25519 keys; no new library dependency; forward secrecy with ephemeral DH.
- Con: we write the handshake (but it's ~30 lines); no library audit.

**Option D — Reuse W#23 pairing HMAC secret**
W#23 issued an HMAC-SHA256 secret at pairing time. Derive session key from that via HKDF.
- Pro: no new handshake; reuses established trust.
- Con: pairing was designed for iOS → Anchor. Anchor → Anchor comms needs peer-mutual trust; the pairing model doesn't extend directly. Would need a new "cross-node pairing" ceremony.

### Decision: Option C — ephemeral X25519 DH + HKDF-SHA256 + ChaCha20-Poly1305

**Rationale:**

- Ed25519 → X25519 key conversion is standardized (RFC 7748 / IETF draft); .NET 6+ supports both natively.
- `ChaCha20Poly1305` is in `System.Security.Cryptography` — zero new dependencies.
- Forward secrecy: ephemeral DH keys discarded after session setup; compromise of long-term keys doesn't expose past sessions.
- The relay sees ciphertext only (per ADR 0031 posture) — this is preserved.
- The handshake fits naturally into the framing protocol as the `HELLO` exchange extended with DH ephemeral keys.

**Extended HELLO for encrypted sessions:**
```
HELLO payload (encrypted session setup):
{
    EphemeralPublicKey: bytes[32],  // X25519 ephemeral public key
    PresenceHeartbeat: { ... }      // capability advertisement (encrypted after DH)
}
```

After `HELLO` exchange, both sides derive the session key:
```
sharedSecret = X25519(myEphemeralPrivate, theirEphemeralPublic)
sessionKey   = HKDF-SHA256(sharedSecret, salt="sunfish-crew-comms-v1", info=concat(myPeerId, theirPeerId))
```

All subsequent frames (INVITE, TEXT, etc.) are encrypted as:
```
[Nonce (12B)] [ChaCha20Poly1305(sessionKey, nonce, plainFrame)]
```
Nonce: 12-byte counter, incremented per-message, prepended to the ciphertext.

---

## §6 `foundation-channels` Contract Surface Draft

**Namespace:** `Sunfish.Foundation.Channels`
**Project:** `packages/foundation-channels/Sunfish.Foundation.Channels.csproj`
**Dependencies:** `foundation`, `foundation-transport` (for `PeerId`/`TransportTier`), `foundation-multitenancy` (for `TenantId`)

```csharp
// Capability flags — what a channel provider can do
[Flags]
public enum ChannelCapability : byte
{
    None  = 0,
    Text  = 1 << 0,
    Audio = 1 << 1,
    Video = 1 << 2,
}

// Presence status — v1 uses Available + Offline; Busy added when multi-session lands
public enum PresenceStatus
{
    Offline,     // not reachable / TTL expired
    Available,   // reachable, not in a session
    Busy,        // reachable, already in a session
}

// Presence record — one crew member visible on the roster
public sealed record CrewPresence
{
    public required PeerId Peer               { get; init; }
    public required TenantId Tenant           { get; init; }
    public required string DisplayName        { get; init; }
    public required ChannelCapability Caps    { get; init; }
    public required PresenceStatus Status     { get; init; }
    public required TransportTier Via         { get; init; }
    public required DateTimeOffset LastSeenAt { get; init; }
}

// Session state
public enum ChannelSessionState
{
    Connecting,   // INVITE sent, awaiting ACCEPT
    Active,       // ACCEPT received, data flowing
    Terminated,   // BYE or timeout
}

// Termination reason
public enum ChannelTerminationReason
{
    LocalBye,
    RemoteBye,
    InviteTimeout,
    TransportError,
}

// Active channel session — owns the IDuplexStream lifetime
public interface IChannelSession : IAsyncDisposable
{
    PeerId Peer                 { get; }
    ChannelCapability Capability { get; }
    ChannelSessionState State    { get; }

    // Text (Phase 1 + 2)
    Task SendTextAsync(string message, CancellationToken ct);
    IAsyncEnumerable<string> ReceiveTextAsync(CancellationToken ct);

    // Audio (Phase 3 stub — implemented in blocks-crew-comms Phase 3)
    Task SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct);

    // Lifecycle
    event EventHandler<ChannelTerminationReason> Terminated;
    Task CloseAsync(CancellationToken ct);
}

// Pending invitation from a remote peer
public interface IChannelInvitation
{
    PeerId FromPeer                        { get; }
    IReadOnlyList<ChannelCapability> OfferedCapabilities { get; }  // priority-ordered from INVITE
    Task<IChannelSession> AcceptAsync(CancellationToken ct);       // accepts at highest mutually supported level
    Task RejectAsync(string? reason, CancellationToken ct);
}

// Top-level provider — one registration per DI container
public interface IChannelProvider
{
    ChannelCapability Capabilities { get; }

    // Presence
    Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct);

    // Outbound
    Task<IChannelSession> OpenAsync(TenantId tenant, PeerId peer, ChannelCapability capability, CancellationToken ct);

    // Inbound
    IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct);
}
```

**What this surface deliberately excludes:**
- Message history / persistence (that's `blocks-messaging` — different concern)
- Group sessions (Phase 1 is 1:1 only)
- Status / DND modes (v2)
- Call quality metrics (v2)
- Push notification for offline peers (v2)

---

## §7 `blocks-crew-comms` Reference Implementation Sketch

**Namespace:** `Sunfish.Blocks.CrewComms`
**Project:** `packages/blocks-crew-comms/Sunfish.Blocks.CrewComms.csproj`
**Dependencies:** `foundation-channels`, `foundation-transport`, `foundation-multitenancy`, `System.Security.Cryptography` (BCL)

**Internal structure:**
```
NativeChannelProvider : IChannelProvider
  ├── PresenceBus          — heartbeat timer + peer roster (TTL-eviction)
  ├── SessionListener      — IAsyncEnumerable<IChannelInvitation> via Channel<T>
  ├── SessionInitiator     — OpenAsync → DH handshake → INVITE → wait ACCEPT
  ├── EncryptionHandshake  — ephemeral X25519 + HKDF session-key derivation
  └── FrameProtocol        — length-prefix + MessagePack encode/decode

NativeChannelSession : IChannelSession
  ├── holds IDuplexStream
  ├── reads encrypted frames on background Task
  ├── routes to text/audio/video IAsyncEnumerable<T> via Channel<T>
  └── JitterBuffer (Phase 3) — adaptive 20–80ms depth, 40ms default; feeds ReceiveAudioFramesAsync
```

**DI registration (in Anchor's `MauiProgram.cs`):**
```csharp
services.AddSunfishCrewComms(); // registers NativeChannelProvider as IChannelProvider
```

---

## §8 Phase Delivery Plan

| Phase | Deliverable | Transport tier | New in this phase |
|---|---|---|---|
| 1 | LAN text chat | mDNS + TCP (Tier 1) | `foundation-channels` contracts + `blocks-crew-comms` NativeChannelProvider + PresenceBus + EncryptionHandshake + text framing + Anchor UI wiring |
| 2 | Cross-network text | Bridge relay (Tier 3) | Zero new channel code — `ITransportSelector` handles tier fallback; integration test with relay URL configured |
| 3 | Audio | Any tier | Opus via `Concentus`; `AUDIO_FRAME` + `MUTE_STATE` activated; adaptive jitter buffer (40ms default); **push-to-talk default** (avoids AEC complexity); OS-level AEC (`AVAudioSession` / `AudioGraph` / `AUVoiceProcessingIO`) for always-on in Phase 3.1 |
| 4 | Video | Any tier | Follow-on workstream; H.264/VP8; `VIDEO_FRAME` message type activated |

**Phase 1 acceptance criteria:**
1. Two Anchor instances on same LAN can see each other in the crew roster within 30s of startup.
2. One crew member can initiate a text session; the other receives an invitation.
3. After accept, messages flow bidirectionally; all frames encrypted (ChaCha20-Poly1305).
4. Session terminates cleanly on BYE from either side.
5. Presence roster updates when a node shuts down (TTL eviction within 90s).

**Phase 2 acceptance criteria:**
1. Same text chat test passes with both nodes on different networks (relay URL configured).
2. `ITransportSelector` selects Tier 3; session encrypted end-to-end; relay sees ciphertext only.

---

## §9 Open Questions for Stage 02 Architecture

1. **`foundation-channels` package dependencies:** Should `foundation-channels` depend on `foundation-transport` directly (for `PeerId` + `TransportTier` types), or should those types be re-exported from `federation-common`? Check for circular dep risk.

2. **Heartbeat transport:** The presence heartbeat needs to reach peers without an open session. For LAN this is easy (mDNS + a new TCP connect per peer). For relay, a heartbeat to every known crew member would open a relay connection per peer — expensive. Consider a shared "presence stream" per relay connection vs individual peer connections.

3. **Crew roster source:** `GetPresentCrewAsync` needs to know *which* peers to send heartbeats to. This requires the tenant's crew member list (PeerId → DisplayName). Does `foundation-channels` take an `ICrewRoster` dependency, or does the caller supply the roster? Decision affects DI shape.

4. **Audio library:** Confirm `Concentus` (pure-managed Opus encoder/decoder, NuGet) is the right choice for Phase 3. Alternative is native `libopus` via P/Invoke. `Concentus` is slower but avoids native binary distribution across MAUI platforms.

5. **mDNS capability TXT records:** As an optimization, extend `MdnsPeerTransportOptions` to carry capability TXT records in Phase 1. This requires a minor addition to `foundation-transport` — coordinate with W#30 owner.

---

## §10 Summary

| Question | Decision |
|---|---|
| Presence model | Push heartbeat (30s period, 90s TTL) + mDNS TXT as LAN fast-path |
| Presence status | `PresenceStatus` enum: `Offline` / `Available` / `Busy` |
| Signaling | On-stream framing; INVITE/ACCEPT/REJECT/BYE + TYPING + DELIVERED + MUTE_STATE |
| Capability negotiation | INVITE carries priority-ordered `capabilities[]`; ACCEPT confirms negotiated level |
| Message identity | Every TEXT frame carries a `MessageId` (UUID); DELIVERED ack closes the loop |
| Frame encoding | Length-prefix (4B LE) + 1B type + MessagePack payload |
| Encryption | Ephemeral X25519 DH + HKDF-SHA256 + ChaCha20-Poly1305 (BCL only) |
| Audio codec | Opus via `Concentus` (Phase 3; confirm in Stage 03) |
| Audio Phase 3 defaults | Push-to-talk MVP; OS-level AEC for always-on (Phase 3.1); 40ms jitter buffer |
| Provider interface | `IChannelProvider` — presence + open + listen |
| Session interface | `IChannelSession` — text send/receive + audio stub + lifecycle events |
| Package split | `foundation-channels` (contracts) + `blocks-crew-comms` (impl) |
| Default-installed | Yes — registered in Anchor `MauiProgram.cs` |

**Exit criteria met:** dependency mapping complete, presence protocol decided, signaling state machine specified, encryption approach decided, contract surface drafted, phase plan with acceptance criteria defined. Ready for Stage 02 Architecture.
