# Crew Comms

`Sunfish.Blocks.CrewComms` is the native reference implementation of the
crew-comms substrate — real-time peer-to-peer messaging between authenticated
crew members on the same tenant. Default-installed in Anchor.

It implements [ADR 0076 — Crew Comms](../../../docs/adrs/0076-crew-comms-foundation-channels.md)
plus amendments A1 (wire-encoding ratification) and A2 (capability-negotiation
verification).

## Phase 1 surface (text-only)

| Type | Role |
|---|---|
| `IChannelProvider` | Public surface: open/listen/presence (in `Sunfish.Foundation.Channels`). |
| `IChannelSession` | Active session between two peers; text send/receive + close + termination reason. |
| `IChannelInvitation` | Inbound invitation: `FromPeer`, `OfferedCapabilities`, `AcceptAsync` / `RejectAsync`. |
| `ICrewRoster` | Tenant directory — the authoritative list of peers a tenant may invite. |
| `CrewPresence` | Live presence record (peer + display name + caps + via-tier + last-seen). |
| `ChannelCapability` | Flags: `None`, `Text`, `Audio` (Phase 3), `Video` (Phase 4). |

## DI registration

```csharp
services.AddSunfishCrewComms(roster =>
    roster.AddInMemory(new[]
    {
        new CrewMember { Peer = alice.PeerId, DisplayName = "Alice" },
        new CrewMember { Peer = bob.PeerId,   DisplayName = "Bob"   },
    }));
```

`AddSunfishCrewComms` registers `NativeChannelProvider` as the singleton
`IChannelProvider`. The local `KeyPair` is registered via `TryAddSingleton`,
so callers MAY pre-register a persistent `KeyPair` (loaded from secure
storage) before calling `AddSunfishCrewComms` to override the per-container
fresh-keygen Phase-1 stub.

`ITransportSelector` is a separate dependency — register via
`services.AddSunfishTransport()` (or equivalent) before this call.

## Wire-protocol primer

Each session is a single `IDuplexStream` (mDNS LAN, mesh-VPN tunnel, or
Bridge-relay HTTPS pipe) framed as `[len(4 LE) | type(1) | payload]`.

Pre-handshake (HELLO only):
- `0x01 HELLO` — plaintext; carries ephemeral X25519 + identity Ed25519 keys
  + tenant binding + Ed25519 signature + embedded presence beacon.

Post-handshake (everything else):
- `[Nonce(12) | ChaCha20-Poly1305(sessionKey, nonce, plaintext)]`
- Session key = HKDF-SHA256(X25519(myEphemeralPriv, peerEphemeralPub),
  salt = `"sunfish-crew-comms-v1"`, info = `peerA:peerB`).
- Role-split nonce: initiator-to-responder bit 63 = 0; responder-to-initiator
  bit 63 = 1 (prevents nonce reuse without state synchronization).

## Encryption handshake (per ADR 0076 §A1)

```
1. Both peers generate ephemeral X25519 key pair.
2. Send signed HELLO: { ephemPub, idPub, tenantId, sig, presence }
3. Verify peer HELLO: signature + tenant + roster membership.
4. Derive session key (X25519 + HKDF-SHA256).
5. Initiator sends INVITE { capabilities }; responder reads.
6. Responder sends ACCEPT { capability }; initiator reads.
7. Initiator verifies ACCEPT.capability ⊆ INVITE.capabilities (per A2).
8. Both sides compute identical CONFIRM transcript hash; exchange CONFIRM.
9. Mismatch → REJECT + close. Match → ACTIVE.
```

## Phases

| Phase | Scope |
|---|---|
| 1 (LAN text) | mDNS-discovered same-network peers; text-only sessions. (Phase 1 substrate complete.) |
| 2 (relay text) | Bridge-relay fallback for peers off the LAN; same wire protocol; ciphertext-only on relay. |
| 3 (audio) | Opus-encoded audio frames (`0x20 AUDIO_FRAME`); push-to-talk; priority-aware send loop required. |
| 4 (video) | H.264/VP8 video frames (`0x30 VIDEO_FRAME`); future. |

## P4.5 deferrals

Tracked in `xo-directive-2026-05-05T11-00Z-w45-p4-path-c-prime.md`:

- **Glare resolution wiring** — `GlareResolver.IsLocalYielder` exists but the
  cross-component coordination (initiator + listener) is deferred. Concurrent
  dial-each-other currently produces two sessions per peer.
- **DELIVERED ack** — Phase 1 acceptance criterion #4. Session reads inbound
  TEXT but does not emit DELIVERED on receipt.
- **TYPING indicator** — Phase 1 acceptance criterion #5. No `SendTypingAsync`
  surface yet.
- ~~**Transcript-hash A1 binding** — `presenceCapsA + presenceCapsB` and A2's
  `inviteCaps` byte are not yet folded into `ComputeTranscriptHash`.~~
  **Closed in P4.5 PR 1.** `ComputeTranscriptHash` now takes 9 parameters
  (`inviteCapabilities`, `negotiatedCapability`, `presenceCapsInitiator`,
  `presenceCapsResponder`) per ADR 0076-A1+A2 canonical form; both callers
  in `HandshakeFlow` (`InitiatorPostHelloAsync` + `ResponderAcceptAsync`)
  pass the corresponding bytes from the wire payloads. Three known-answer
  vectors from `tools/icm/channel-test-vectors.json` (V7/V8/V9) pin the
  binding.

## Audio (Phase 3)

Phase 3 introduces 50 fps Opus audio frames — at this rate, a 20 ms audio
send must not block control frames (HEARTBEAT, BYE, CONFIRM). The current
`FrameProtocol.SemaphoreSlim` write gate must upgrade to a priority-aware
`Channel<WriteRequest>` producer/consumer pattern that drains all control
frames (`type ≤ 0x09`) before data frames (`type ≥ 0x10`) each iteration.

Audio codec: [Concentus](https://github.com/lostromb/concentus) (managed-only
Opus; no native dependency). Not yet in `Directory.Packages.props`.

## Security

- **End-to-end encrypted.** The Bridge relay (Tier 3) handles only
  ciphertext — preserves ADR 0031 tenant-data-isolation posture.
- **Forward secrecy.** Ephemeral X25519 + HKDF derives a fresh session key per
  connection. No session resumption; prior session keys zeroed on
  `CloseAsync` / `DisposeAsync`. Compromise of a peer's long-term Ed25519
  identity key does NOT expose past session traffic.
- **HELLO authentication.** Both ephemeral pubkeys are signed with the
  sender's Ed25519 long-term key; receivers verify the signature before
  computing the shared secret.
- **Tenant binding.** HELLO includes `tenantId` (UTF-8 bytes of
  `TenantId.Value`); receivers reject HELLOs from peers not in their
  tenant roster.
- **Capability-downgrade detection (per A2).** Initiator verifies
  `ACCEPT.capability ⊆ INVITE.capabilities`; rejects + throws on mismatch.
- **Bounded INVITE queue.** Listener uses
  `Channel.CreateBounded<IChannelInvitation>(16)` with `Wait` mode;
  saturation drops the new INVITE and emits `ChannelInviteDropped` audit.
