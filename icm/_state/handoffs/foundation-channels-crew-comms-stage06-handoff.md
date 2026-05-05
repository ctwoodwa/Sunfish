# Stage 06 Hand-off — W#45 Crew Comms (`foundation-channels` + `blocks-crew-comms`)

**Workstream:** W#45 — Crew Comms
**ADR:** [`docs/adrs/0076-crew-comms-foundation-channels.md`](../../docs/adrs/0076-crew-comms-foundation-channels.md) (Accepted; council-amended 2026-05-04)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~15–18h sunfish-PM time / 5 PRs / 5 phases
**Cohort posture:** pre-merge council canonical for Phases 2 + 4 (crypto primitives + integration wiring); standard review for Phases 1 / 3 / 5.

---

## Summary

Build `foundation-channels` (thin contracts package) and `blocks-crew-comms` (native reference implementation, default-installed in Anchor). Delivers real-time crew text chat as an alternative Anchor MVP path: LAN-local first (Phase 1), then cross-network via Bridge relay (Phase 2). Protocol is E2E encrypted; relay sees only ciphertext.

**Hard prerequisite:** `foundation-transport` (W#30, `Sunfish.Foundation.Transport`) must be merged and buildable — confirmed on `origin/main` as of 2026-05-04.

---

## Critical implementation notes (read before writing any code)

### 1. FrameProtocol write serialization — required

`WebSocketDuplexStream` wraps `ClientWebSocket.SendAsync`. .NET supports one outstanding send and one outstanding receive simultaneously — concurrent sends from separate Tasks throw `InvalidOperationException`. Multiple components write to the same stream concurrently (HEARTBEAT timer, TEXT sends, INVITE/ACCEPT/CONFIRM signaling). **`FrameProtocol` MUST gate all writes behind a `SemaphoreSlim(1, 1)`.** Apply defensively to TCP streams too.

```csharp
// Inside FrameProtocol — required pattern (mirrors WebSocketSyncDaemonTransportConnection)
private readonly SemaphoreSlim _sendGate = new(1, 1);

public async Task WriteFrameAsync(byte type, ReadOnlyMemory<byte> payload, CancellationToken ct)
{
    await _sendGate.WaitAsync(ct).ConfigureAwait(false);
    try
    {
        // write length-prefix + type + payload to IDuplexStream
    }
    finally { _sendGate.Release(); }
}
```

**Pattern source:** `packages/kernel-sync/Protocol/WebSocketSyncDaemonTransport.cs:163` (`WebSocketSyncDaemonTransportConnection._sendGate`) and `UnixSocketSyncDaemonTransport.cs:269` (`StreamConnection._sendGate`) — identical approach, established cohort precedent.

**Phase 3 upgrade path:** When audio ships (50fps Opus frames), control frames (HEARTBEAT, BYE, CONFIRM) must not wait behind a 20ms audio frame send. At that point, upgrade `FrameProtocol` from `SemaphoreSlim` to a `Channel<WriteRequest>` producer-consumer pattern with priority: drain all control frames (`type ≤ 0x09`) before data frames (`type ≥ 0x10`) each iteration. The Phase 3 hand-off will specify this. Phase 4 video (256KB frames) requires it absolutely. Do NOT pre-implement the Channel pattern in Phase 1.

### 2. Correct NSec API class names

The ADR mentions `Algorithm.X25519` / `Algorithm.HkdfSha256` / `Algorithm.ChaCha20Poly1305` — these are NOT valid NSec static properties. Use the correct classes:

| Operation | NSec type |
|---|---|
| X25519 key agreement | `KeyAgreementAlgorithm.X25519` |
| HKDF-SHA256 derivation | `KeyDerivationAlgorithm.HkdfSha256` |
| ChaCha20-Poly1305 AEAD | `AeadAlgorithm.ChaCha20Poly1305` |
| Ed25519 sign / verify | `SignatureAlgorithm.Ed25519` (already used in `Sunfish.Foundation.Crypto`) |

**NSec package:** `NSec.Cryptography 26.1.0-preview.1` is already in `Directory.Packages.props`; add `<PackageReference Include="NSec.Cryptography" />` to `blocks-crew-comms.csproj` (no version needed).

### 3. PeerId IS the Ed25519 public key — use this for HELLO verification

`PeerId.Value` is the **base64url-encoded 32-byte Ed25519 public key** (`PeerId.From(PrincipalId)` in `federation-common/PeerId.cs`). HELLO carries `identityPublicKey: bytes[32]`. Verification:

```csharp
// Receiver verifies HELLO.identityPublicKey belongs to a roster member:
var principal = PrincipalId.FromBytes(hello.IdentityPublicKey);   // in Sunfish.Foundation.Crypto
var peerId    = PeerId.From(principal);                            // in Sunfish.Federation.Common
var roster    = await _crewRoster.GetCrewAsync(tenantId, ct);
if (!roster.Any(m => m.Peer == peerId)) { /* reject */ }
```

No `CrewMember.Ed25519PublicKey` field needed. No `ICrewKeyStore` interface needed.

### 4. KeyPair lifetime and disposal

`NativeChannelProvider` is singleton-scoped. Inject a `KeyPair` (from `Sunfish.Foundation.Crypto`) via constructor; the provider holds it for its lifetime. `KeyPair` is `IDisposable` — NSec zeroes key material on dispose. Wire `NativeChannelProvider : IAsyncDisposable` (or `IDisposable`) and dispose `KeyPair` in `Dispose`.

```csharp
services.AddSingleton<KeyPair>(_ => KeyPair.Generate());
services.AddSingleton<IChannelProvider, NativeChannelProvider>();
```

### 5. SharedSecret dispose pattern in EncryptionHandshake

`KeyAgreementAlgorithm.X25519.Agree()` returns a `SharedSecret?` that holds raw DH output in unmanaged memory. It must be disposed immediately after `KeyDerivationAlgorithm.HkdfSha256.DeriveKey()` extracts the session key:

```csharp
using var sharedSecret = KeyAgreementAlgorithm.X25519.Agree(myEphemeralKey, theirPublicKey)
    ?? throw new CryptographicException("X25519 agreement failed — null shared secret");
var sessionKey = KeyDerivationAlgorithm.HkdfSha256.DeriveKey(
    sharedSecret,
    salt: "sunfish-crew-comms-v1"u8,
    info: BuildInfo(initiatorPeerId, responderPeerId),
    algorithm: AeadAlgorithm.ChaCha20Poly1305,
    extractParameters: new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
// sharedSecret disposed by `using`; sessionKey now owns the derived material
```

Zero `sessionKey` in `EncryptionHandshake.Dispose()`.

---

## Phase 1 — `foundation-channels` contracts package (~2h, 1 PR)

**New package:** `packages/foundation-channels/Sunfish.Foundation.Channels.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Foundation.Channels</PackageId>
    <RootNamespace>Sunfish.Foundation.Channels</RootNamespace>
    <Description>Contracts for real-time crew communication channels (ADR 0076).</Description>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\foundation-transport\Sunfish.Foundation.Transport.csproj" />
    <ProjectReference Include="..\foundation-multitenancy\Sunfish.Foundation.MultiTenancy.csproj" />
    <ProjectReference Include="..\federation-common\Sunfish.Federation.Common.csproj" />
  </ItemGroup>
</Project>
```

**Files to create** (all in `packages/foundation-channels/`, namespace `Sunfish.Foundation.Channels`):

- `ChannelCapability.cs`
```csharp
[Flags]
public enum ChannelCapability : byte { None = 0, Text = 1 << 0, Audio = 1 << 1, Video = 1 << 2 }
```

- `PresenceStatus.cs`
```csharp
public enum PresenceStatus { Offline, Available, Busy }
```

- `CrewPresence.cs` — `sealed record CrewPresence { PeerId, TenantId, DisplayName, ChannelCapability Caps, PresenceStatus, TransportTier Via, DateTimeOffset LastSeenAt }` (all `required`)

- `CrewMember.cs` — `sealed record CrewMember { PeerId Peer, string DisplayName }` (all `required`)

- `ICrewRoster.cs`
```csharp
public interface ICrewRoster
{
    Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct);
}
```

- `ChannelSessionState.cs` — `enum ChannelSessionState { Connecting, Active, Terminated }`

- `ChannelTerminationReason.cs` — `enum ChannelTerminationReason { LocalBye, RemoteBye, InviteTimeout, TransportError, TranscriptMismatch }`

- `IChannelSession.cs` — per ADR 0076 §foundation-channels contract surface. `Completed` is `Task<ChannelTerminationReason>`. `ReceiveTextAsync` single-consumer contract in XML doc. Phase 3 audio stubs with `NotSupportedException` contract in XML doc.

- `IChannelInvitation.cs` — per ADR 0076. `OfferedCapabilities` type is `ChannelCapability` (flags), not a list.

- `IChannelProvider.cs` — per ADR 0076. `OpenAsync` takes `ChannelCapability preferredCapabilities` (flags). `ListenAsync` XML doc: bounded Channel(16), drop audit event on full.

**No tests in this phase** — contracts only; tested implicitly by Phase 4 integration.

**Acceptance gate (binary):** `dotnet build packages/foundation-channels` clean; all 9 type files present; `IChannelProvider`, `IChannelSession`, `IChannelInvitation`, `ICrewRoster` match the ADR 0076 contract surface exactly.

---

## Phase 2 — `EncryptionHandshake` + `FrameProtocol` (~3–4h, 1 PR — council required)

**New package:** `packages/blocks-crew-comms/Sunfish.Blocks.CrewComms.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Blocks.CrewComms</PackageId>
    <RootNamespace>Sunfish.Blocks.CrewComms</RootNamespace>
    <Description>Native crew communication reference implementation for Anchor (ADR 0076).</Description>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MessagePack" />
    <PackageReference Include="NSec.Cryptography" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation-channels\Sunfish.Foundation.Channels.csproj" />
    <ProjectReference Include="..\foundation-transport\Sunfish.Foundation.Transport.csproj" />
    <ProjectReference Include="..\foundation-multitenancy\Sunfish.Foundation.MultiTenancy.csproj" />
    <ProjectReference Include="..\federation-common\Sunfish.Federation.Common.csproj" />
  </ItemGroup>
</Project>
```

**Files to create:**

- `packages/blocks-crew-comms/Protocol/MessageType.cs` — byte constants for all 12 message types per ADR 0076 wire protocol table (HELLO=0x01, HEARTBEAT=0x02, INVITE=0x03, ACCEPT=0x04, REJECT=0x05, CONFIRM=0x0A, BYE=0x06, TYPING=0x07, DELIVERED=0x08, MUTE_STATE=0x09, TEXT=0x10, AUDIO_FRAME=0x20, VIDEO_FRAME=0x30)

- `packages/blocks-crew-comms/Protocol/FrameProtocol.cs` — length-prefix framing (4B LE + 1B type + payload); `SemaphoreSlim(1,1)` write gate (required — see Critical Note §1); `ReadFrameAsync` / `WriteFrameAsync`; MessagePack encode/decode per frame type; RFC 4122 big-endian UUID encoding for all UUID fields (NOT `Guid.ToByteArray()`).

  UUID encoding helper:
  ```csharp
  // Write Guid as RFC 4122 big-endian (16 bytes)
  static void WriteGuidBigEndian(Span<byte> dest, Guid g)
  {
      BinaryPrimitives.WriteUInt32BigEndian(dest[..4],  (uint)(g.GetHashCode())); // wrong — use proper decomposition
      // Correct: decompose via g.ToString("N") → parse hex components → write each in big-endian
      // Or: use a custom MessagePack formatter that normalizes to RFC 4122
  }
  ```
  Recommendation: write a `GuidFormatter : IMessagePackFormatter<Guid>` that always serializes via `Guid.ToString("N")` parse → RFC 4122 component layout.

- `packages/blocks-crew-comms/Protocol/HelloPayload.cs` — MessagePack-tagged record: `EphemeralPublicKey: byte[]` (32), `IdentityPublicKey: byte[]` (32), `TenantId: byte[]` (16, RFC 4122), `Signature: byte[]` (64), `Presence: PresenceHeartbeat`

- `packages/blocks-crew-comms/Protocol/PresenceHeartbeat.cs` — MessagePack-tagged record: `PeerId: string`, `TenantId: byte[]` (16), `Caps: byte`, `Timestamp: long` (Unix ms), `Signature: byte[]` (64)

- `packages/blocks-crew-comms/Protocol/InvitePayload.cs` — `Capabilities: byte` (flags-combined)

- `packages/blocks-crew-comms/Protocol/AcceptPayload.cs` — `Capability: byte`

- `packages/blocks-crew-comms/Protocol/ConfirmPayload.cs` — `TranscriptHash: byte[]` (32)

- `packages/blocks-crew-comms/Protocol/TextPayload.cs` — `MessageId: byte[]` (16, RFC 4122), `Message: string`

- `packages/blocks-crew-comms/Protocol/DeliveredPayload.cs` — `MessageId: byte[]` (16, RFC 4122)

- `packages/blocks-crew-comms/Crypto/EncryptionHandshake.cs` — performs full handshake per ADR 0076 §Encryption handshake steps 1–9:
  - Generates ephemeral X25519 key via `KeyAgreementAlgorithm.X25519`
  - Signs HELLO data (`ephemeralPublicKey || identityPublicKey || tenantId`) with `SignatureAlgorithm.Ed25519` using the injected `KeyPair`
  - Verifies remote HELLO Ed25519 signature using `PublicKey.Import(SignatureAlgorithm.Ed25519, identityPublicKey, KeyBlobFormat.RawPublicKey)`
  - Verifies remote peer is in roster: `PrincipalId.FromBytes(identityPublicKey)` → `PeerId.From(...)` → roster lookup
  - Computes `SharedSecret` via `KeyAgreementAlgorithm.X25519.Agree()`; derives session key via `KeyDerivationAlgorithm.HkdfSha256.DeriveKey()` — disposes `SharedSecret` immediately
  - Returns `sessionKey: Key` (owned; caller disposes on session close)
  - Computes `transcriptHash` post-ACCEPT and sends CONFIRM; verifies remote CONFIRM
  - Zeroes `sessionKey` in `Dispose()`

**Tests** (in `packages/blocks-crew-comms/tests/Sunfish.Blocks.CrewComms.Tests.csproj`):

- `FrameProtocol_RoundTrip` — encode + decode each of the 12 message types; verify payloads match
- `FrameProtocol_UuidEncoding` — encode `Guid.NewGuid()` as TEXT messageId → decode → byte-compare against RFC 4122 big-endian layout (NOT `Guid.ToByteArray()` layout)
- `FrameProtocol_WriteLock` — two tasks call `WriteFrameAsync` concurrently over a `WebSocketDuplexStream` stub → no exception, both frames written sequentially
- `EncryptionHandshake_SharedSecretAgreement` — two handshakes (initiator + responder) agree on identical session key bytes
- `EncryptionHandshake_HelloSignatureVerified` — tampered `identityPublicKey` in HELLO → `CryptographicException` thrown
- `EncryptionHandshake_TenantRosterRejection` — peer not in roster → rejected before session proceeds
- `EncryptionHandshake_ConfirmHashMatch` — both sides compute same `transcriptHash` in nominal flow
- `EncryptionHandshake_ConfirmMismatchRejects` — injected transcript-hash mismatch → `ChannelTerminationReason.TranscriptMismatch`

**Acceptance gate (binary):** all 8 tests green; `dotnet build` clean; no raw `Guid.ToByteArray()` calls in MessagePack serialization paths.

---

## Phase 3 — `PresenceBus` + `NativeChannelSession` + signaling (~5–6h, 1 PR)

**Files to create:**

- `packages/blocks-crew-comms/Presence/PresenceBus.cs`
  - 30s heartbeat timer; sends signed HEARTBEAT over all open sessions
  - TTL-eviction roster: 45s timeout from `LastSeenAt` (not 90s)
  - 20s in-session keepalive: if no frame sent for 20s within an ACTIVE session, send HEARTBEAT on that stream
  - mDNS fast-path: seed initial presence from `MdnsPeerTransport` peer cache via `ITransportSelector`
  - Phase 2 speculative relay HELLO: for each roster peer not yet present, attempt relay probe via `IPeerTransport.ConnectAsync`; probe timeout 10s; max 3 concurrent probes (bounded by `SemaphoreSlim(3,3)`)
  - Exposes `IReadOnlyList<CrewPresence> GetSnapshot()` for `NativeChannelProvider.GetPresentCrewAsync`

- `packages/blocks-crew-comms/Session/NativeChannelSession.cs`
  - Implements `IChannelSession`
  - Holds `IDuplexStream` (owned; disposed on `CloseAsync`)
  - `Completed: Task<ChannelTerminationReason>` — backed by `TaskCompletionSource<ChannelTerminationReason>`
  - Dedicated reader Task runs background pump: decrypts frames, routes by type
  - `ReceiveTextAsync` backed by `Channel<string>` (unbounded single-consumer; throws if second consumer detected)
  - `CloseAsync`: sends BYE → 2s drain wait → disposes `IDuplexStream` → completes `Completed`
  - `DisposeAsync`: if `CloseAsync` not yet called, sends BYE fire-and-forget → disposes immediately

- `packages/blocks-crew-comms/Session/SessionState.cs` — state machine enum: `Idle / Inviting / Confirming / Active / Terminated`; internal use only

- `packages/blocks-crew-comms/Signaling/GlareResolver.cs` — static method; takes two `PeerId` values; returns which is the initiator:
  ```csharp
  // Lower PeerId.Value (ordinal) yields — other is initiator
  static bool IsLocalYielder(PeerId local, PeerId remote)
      => string.CompareOrdinal(local.Value, remote.Value) < 0;
  ```

**Tests:**

- `NativeChannelSession_StateTransitions` — Idle → Inviting → Confirming → Active → Terminated via mocked stream
- `NativeChannelSession_GlareResolution` — two sessions simultaneously INVITE; lower PeerId yields; session transitions to INVITED
- `NativeChannelSession_InviteTimeout` — INVITE not answered within 60s → `ChannelTerminationReason.InviteTimeout`
- `PresenceBus_TtlEviction` — peer heartbeat stops; roster evicts after 45s
- `PresenceBus_InSessionKeepalive` — 20s of silence on active session → HEARTBEAT sent on that stream
- `PresenceBus_BoundedRelayProbe` — 10 roster peers, all relay-only; only 3 probes run concurrently

**Acceptance gate (binary):** all 6 tests green; state machine covers all transitions including glare.

---

## Phase 4 — `SessionInitiator` + `SessionListener` + `NativeChannelProvider` + DI + integration test (~3–4h, 1 PR — council required)

**Files to create:**

- `packages/blocks-crew-comms/Signaling/SessionInitiator.cs`
  - `OpenAsync(TenantId, PeerId, ChannelCapability, CancellationToken)` → `IChannelSession`
  - Connects via `ITransportSelector.SelectAsync` → `IPeerTransport.ConnectAsync` → `IDuplexStream`
  - Full HELLO exchange (via `EncryptionHandshake`); sends INVITE; waits ACCEPT (60s timeout); exchanges CONFIRM; → ACTIVE

- `packages/blocks-crew-comms/Signaling/SessionListener.cs`
  - `ListenAsync(TenantId, CancellationToken)` → `IAsyncEnumerable<IChannelInvitation>`
  - Backed by `Channel.CreateBounded<IChannelInvitation>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropNewest })`
  - On drop: emits `ChannelInviteDropped` audit event via `IAuditTrail`
  - Each incoming `IChannelInvitation.AcceptAsync()` completes the CONFIRM exchange before returning the session

- `packages/blocks-crew-comms/NativeChannelProvider.cs`
  - Implements `IChannelProvider`
  - Constructor injects: `KeyPair`, `ICrewRoster`, `ITransportSelector`, `IAuditTrail?`, `ILogger<NativeChannelProvider>?`
  - `GetPresentCrewAsync` delegates to `PresenceBus.GetSnapshot()`
  - `OpenAsync` delegates to `SessionInitiator`
  - `ListenAsync` delegates to `SessionListener`
  - `IDisposable`: disposes `KeyPair`, stops `PresenceBus` timer

- `packages/blocks-crew-comms/InMemoryCrewRoster.cs` — `ICrewRoster`; configurable seed list of `CrewMember`; used in Anchor Phase 1 and all tests

- `packages/blocks-crew-comms/DependencyInjection/ServiceCollectionExtensions.cs`
  ```csharp
  public static IServiceCollection AddSunfishCrewComms(
      this IServiceCollection services,
      Action<CrewCommsBuilder> configure)
  {
      var builder = new CrewCommsBuilder(services);
      configure(builder);
      services.AddSingleton<KeyPair>(_ => KeyPair.Generate());
      services.AddSingleton<IChannelProvider, NativeChannelProvider>();
      return services;
  }
  ```

- `packages/blocks-crew-comms/DependencyInjection/CrewCommsBuilder.cs`
  - `.AddInMemory(IEnumerable<CrewMember> seed)` → registers `InMemoryCrewRoster` as `ICrewRoster`

**Integration test** (in existing test project, separate file):

- `NativeChannelProvider_EndToEnd_TextExchange` — two `NativeChannelProvider` instances wired with in-memory `IDuplexStream` pair (pipe); one calls `OpenAsync`; other receives via `ListenAsync`; both exchange text messages; verify `DELIVERED` ack received; verify `Completed` fires on `BYE`

**Acceptance gate (binary):** integration test green; `dotnet build` clean; DI registration compiles; `InMemoryCrewRoster` with 2 seeded members supports full end-to-end flow.

---

## Phase 5 — Anchor wiring + docs + ledger flip (~1.5h, 1 PR)

**Files to modify:**

- `accelerators/anchor/MauiProgram.cs` — add `services.AddSunfishCrewComms(roster => roster.AddInMemory(/* TODO: real roster hook */));`
- Confirm `accelerators/anchor/Sunfish.Anchor.csproj` includes `<ProjectReference>` to `blocks-crew-comms`

**Files to create:**

- `apps/docs/blocks/crew-comms/overview.md` — block documentation: purpose, `IChannelProvider` DI registration, `IChannelSession` lifecycle, Phase roadmap, Phase 3 audio note (Concentus)

**Files to modify (transport doc gap — one XML comment addition, no behavior change):**

- `packages/foundation-transport/Relay/WebSocketDuplexStream.cs` — add to class XML doc: `/// <para>Concurrent writes from multiple Tasks are NOT supported — callers must serialize all WriteAsync calls (see <c>WebSocketSyncDaemonTransportConnection._sendGate</c> in kernel-sync for the canonical pattern).</para>`

**Files to modify (state):**

- `icm/_state/active-workstreams.md` — W#45 row: `ready-to-build` → `built`; add PR list; add new package list (`foundation-channels`, `blocks-crew-comms`)
- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_45_crew_comms.md` — update status to built

**Acceptance gate (binary):** Anchor project builds; docs page present; ledger row updated.

---

## Halt-conditions (write `cob-question` beacon to `icm/_state/research-inbox/` if any fire)

1. **Write lock missing** → `FrameProtocol` concurrent-write test throws `InvalidOperationException` on WebSocket stream. **Kill trigger.** Fix before any integration test.
2. **CONFIRM hash mismatch in both-directions test** → serialization bug in transcript computation. **Kill trigger.** All E2E encryption claims void until resolved.
3. **`KeyAgreementAlgorithm.X25519.Agree()` returns null** → platform doesn't support X25519. Halt; ask XO to evaluate NSec version or platform constraint.
4. **`InMemoryCrewRoster` seed requires public-key material** → if roster verification requires more than `PeerId` (e.g., the implementation finds `PrincipalId.FromBytes(identityPublicKey) → PeerId.From(...)` doesn't match roster), halt and describe the specific mismatch. XO will clarify.
5. **`PresenceBus` relay probe causes performance regression** — if speculative HELLO for N roster peers blocks startup for >5s on a relay-only config, reduce probe concurrency to 1 and document.
6. **`Concentus` missing for Phase 3** — NOT a Phase 1/2 halt; document the gap in the ledger note. Phase 3 is a separate hand-off.
7. **`MauiProgram.cs` already registers `IChannelProvider`** — check before Phase 5 wiring; if a stub exists, coordinate with XO.

---

## Cohort patterns to follow

- **`AddSunfishX()` DI extension** with optional audit parameter — per cohort precedent.
- **`JsonStringEnumConverter`** on every public enum for canonical JSON round-trip (per ADR 0028 §A7.8).
- **`NSubstitute`** for test doubles (project default).
- **`ConcurrentDictionary`** for any in-process keyed state.
- **Commit types:** `feat(foundation-channels):` / `feat(blocks-crew-comms):` / `test(crew-comms):` / `docs(crew-comms):` / `chore(crew-comms):`.
- **Pre-merge council:** Phase 2 (crypto) and Phase 4 (integration) — dispatch 4-perspective Stage 1.5 subagents before merging. Cohort batting average 22-of-22.

---

## Cross-references

- **ADR:** [`docs/adrs/0076-crew-comms-foundation-channels.md`](../../docs/adrs/0076-crew-comms-foundation-channels.md)
- **Intake:** [`icm/00_intake/output/2026-05-04_crew-comms-intake.md`](../../icm/00_intake/output/2026-05-04_crew-comms-intake.md)
- **Discovery:** [`icm/01_discovery/output/2026-05-04_crew-comms-discovery.md`](../../icm/01_discovery/output/2026-05-04_crew-comms-discovery.md)
- **Transport substrate:** [`docs/adrs/0061-three-tier-peer-transport.md`](../../docs/adrs/0061-three-tier-peer-transport.md) + `packages/foundation-transport/`
- **Crypto substrate:** `packages/foundation/Crypto/` — `KeyPair`, `PrincipalId`, `Ed25519Signer`, `Ed25519Verifier`, `SignatureAlgorithm.Ed25519`
- **PeerId ↔ Ed25519 binding:** `packages/federation-common/PeerId.cs` — `PeerId.From(PrincipalId)` = base64url of 32-byte public key
- **Sibling hand-offs:** `foundation-wayfinder-stage06-handoff.md` (patterns) + `mesh-vpn-three-tier-transport-stage06-handoff.md` (transport precedent)
