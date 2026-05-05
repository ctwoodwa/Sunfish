# ADR 0076 Amendment A1 Council Review — Pre-merge Canonical (Stage 1.5)

**ADR / Amendment:** [ADR 0076 §A1 — Wire-encoding ratification](../../../../docs/adrs/0076-crew-comms-foundation-channels.md)
**PR under review:** [#548](https://github.com/ctwoodwa/Sunfish/pull/548) — `docs(adrs): 0076 A1 — wire-encoding ratification (unblocks W#45 P2)`
**Branch under review:** `docs/adr-0076-a1-wire-encoding` (stacked on PR #534 base `docs/adr-0076-crew-comms`)
**Reviewer:** XO research subagent (Opus 4.7, `xhigh`)
**Date:** 2026-05-04
**Discipline:** ADR 0069 D1 (pre-merge council canonical for substrate amendments), D2 (§A0 self-audit pressure-test), D3 (three-direction structural-citation spot-check)
**Worktree base:** `origin/main` @ `1e3d889` (post `docs/adrs 0069 implementation` merge)
**Cohort context:** 27-of-28 substrate amendments needed council fixes (~96.4%). ADR 0028 A11 is the cohort's single PASS-on-first-try. This A1 amendment is small substrate (3 wire-encoding ratifications) — the same shape as A11.
**Crypto-correctness emphasis:** transcript-hash is a security-relevant primitive; wire-encoding errors here can enable downgrade / cross-tenant injection / collision-based confusion attacks. Spot-check pressure-tested commensurate with that risk.

---

## Verdict

**NEEDS-AMENDMENT** (not BLOCKING — the three ratification decisions are individually correct; but two structural-citation defects in the rationale prose, one cross-implementation interop risk that the rationale does not address, and one missing crypto-hygiene fixture (test vectors) warrant a fresh push before this flips to merged. Cohort tally remains 27-of-28 if these are applied; A1 lands as the **28th** case where pre-merge council was canonical.)

The three decisions — **(1)** reduced field-extract transcript-hash form, **(2)** UTF-8-bytes `tenantId` wire encoding, **(3)** `bytes[32]` `peerId` wire encoding — are each individually defensible and arguably correct. The reduced form is the right answer to the MessagePack-key-ordering brittleness. The UTF-8 tenant binding is the right answer to "TenantId is string-backed and we are not refactoring every multi-tenant package." The 32-byte raw-key `peerId` is the right answer for HEARTBEAT signature alignment with `identityPublicKey` already in HELLO.

But the **rationale** for decision (3) miscites what `PeerId.From(PrincipalId)` actually produces; the **canonical form** for the transcript-hash is missing a domain-separator prefix (cohort precedent in ADR 0046 uses HKDF-with-info-string for exactly this purpose); and the amendment ships **no test vectors** for either the transcript-hash byte layout or the HEARTBEAT signable, which means cross-implementation interop (the explicit motivating concern for the amendment) cannot be verified by future contributors against an authoritative reference.

If the author applies F1–F4 below, this would be the cohort's **second** counter-example to the pre-merge-fixes-needed pattern. As-is, it is the 28th case where pre-merge council was canonical — same outcome shape as 27 prior amendments.

---

## Findings summary

| Class | Count | IDs |
|---|---|---|
| **Blocking** | 0 | (none — no logic-correctness or layering violations; decisions themselves are sound) |
| **Major** | 4 | F1 (`PeerId.From(PrincipalId)` mis-citation), F2 (missing domain-separator prefix on transcript-hash; cohort drift from ADR 0046), F3 (no test vectors for transcript-hash or HEARTBEAT signable; interop unfalsifiable), F4 (HEARTBEAT signable byte-width annotation incomplete on existing ADR text — `peerId[32]`/`caps[1]`/`timestamp[8 BE]` added but `tenantBytes` length-prefix not mirrored to HEARTBEAT signable) |
| **Minor** | 3 | F5 (length-prefix width inconsistency: `uint32BE(len(tenantBytes))` for transcript-hash vs implicit MessagePack length-prefix on HELLO signable for the same logical field — risk of subtle byte-mismatch), F6 (§A1 prose says "extension attack via variable-length field adjacency" — the actual attack class is "length-extension confusion" or "ambiguous concatenation"; nomenclature drift), F7 (HELLO signable §step 2 does NOT use length-prefix on `UTF8(tenantId.Value)`, but transcript-hash DOES — inconsistency without justification) |
| **Mechanical** | 2 | F8 (Implementation checklist line tail "(A1)" reads ambiguously — clarify), F9 (§A1 #1 capitalizes "Critical" suggesting council finding labels but the council that produced these is COB's local pre-merge on W#45 P2 implementation, not a council on ADR 0076 itself; provenance attribution worth a sentence) |

**Total: 9 findings** (0 Blocking + 4 Major + 3 Minor + 2 Mechanical). Per ADR 0069 D1, all 4 Major findings + the 3 Minor findings warrant a fresh `Status: Proposed` push (NEEDS-AMENDMENT). The 2 mechanical fixes can ride along in the same amendment commit.

**Three-direction structural-citation discipline (per ADR 0069 D3):**
- **Negative existence (does symbol exist that's claimed missing)** — n/a; no negative claims in A1.
- **Positive existence (does symbol exist that's claimed present)** — `Sunfish.Foundation.Assets.Common.TenantId` ✓ verified at `packages/foundation/Assets/Common/TenantId.cs` line 8. `PeerId` ✓ verified at `packages/federation-common/PeerId.cs` line 10. `PeerId.From(PrincipalId)` ✓ verified at line 13 — but the verification surfaces F1 (the rationale's claim that this method "produces the raw 32-byte Ed25519 identity public key" is WRONG; the method produces a `PeerId(string Value)` where `Value` is `principal.ToBase64Url()` — a 43-character base64url string, NOT 32 raw bytes). `PrincipalId` ✓ verified at `packages/foundation/Crypto/PrincipalId.cs` line 28 with `LengthInBytes = 32` and `AsSpan()` accessor.
- **Structural citation (do claimed-existing schema/field shapes match)** — `PeerId.Value` is `string` (base64url-encoded), NOT `bytes[32]`. The amendment's wire-encoding decision (raw `bytes[32]` for `peerId` on the wire) is correct but the bridge from `PeerId.Value` (string) to wire `bytes[32]` requires either (a) storing `PrincipalId` alongside `PeerId` or (b) base64url-decoding `PeerId.Value` at encode time — neither is named in the amendment text. F1 covers this.

---

## Perspective 1 — Outside Observer (fresh-contributor clarity)

**Cold-start reading impression:** Amendment A1 is structurally well-shaped. The three decisions are numbered, each has a one-paragraph rationale, and the relationship to the original draft is explicit ("The original draft specified X. Decision: Y."). A fresh contributor who reads §Wire protocol → §A1 → §Encryption handshake step 9 will understand the changes in roughly that order without re-reading.

The amendment summarizes the implementation alignment ("The implementation already matched the reduced form; the ADR text now matches the implementation") which is valuable provenance for a reader who wonders why the spec is changing post-acceptance. It also explicitly scopes the UUID encoding mandate ("now scoped to `messageId` fields only") and updates the §Wire protocol UUID/GUID encoding paragraph to match. That's the right kind of bookkeeping.

### OO-1 (Major, → F1) — Rationale paragraph for decision (3) miscites `PeerId.From(PrincipalId)`

§A1 #3 reads: *"The implementation's `PeerId.From(PrincipalId)` produces the raw 32-byte Ed25519 identity public key."*

Read against `packages/federation-common/PeerId.cs` line 13:

```csharp
public static PeerId From(PrincipalId principal) => new(principal.ToBase64Url());
```

`PeerId.From(PrincipalId)` produces a `PeerId(string Value)` where `Value` is `principal.ToBase64Url()` — a 43-character base64url-encoded string. It does **not** produce raw bytes. The 32-byte raw key lives on `PrincipalId` (via `PrincipalId.AsSpan()` at line 49 of PrincipalId.cs) or can be recovered from `PeerId.Value` by `Convert.FromBase64String` with the URL-safe alphabet.

This is an OS-level cold-read trap: a fresh COB contributor reading "Decision: `peerId` encodes as raw `bytes[32]`" PLUS "`PeerId.From(PrincipalId)` produces the raw 32-byte Ed25519 identity public key" will assume `PeerId.Value` is the raw bytes and write encoding code that ships base64url ASCII bytes onto the wire, blowing the HEARTBEAT signature byte-by-byte.

**Recommendation:** rewrite the second sentence of §A1 #3 to: *"The implementation's `PeerId` is constructed via `PeerId.From(PrincipalId)`, which sets `PeerId.Value = principal.ToBase64Url()`. The raw 32-byte Ed25519 identity public key lives on the underlying `PrincipalId` (accessible via `PrincipalId.AsSpan()`); wire-encode `peerId` by either (a) carrying `PrincipalId` alongside `PeerId` for sign-and-send paths, or (b) base64url-decoding `PeerId.Value` at encode time."*

Cross-reference: ADR 0061 line 474 already establishes `PeerId` as string-typed: *"`PeerId` (a `readonly record struct PeerId(string Value)` — base64url-encoded Ed25519 public key)"* — so the cohort already has the correct framing; A1 just needs to reuse it.

### OO-2 (Minor, → F9) — Provenance of "1 Critical + 4 Major" findings

§A1 opens with: *"Three wire-encoding choices ratified after P2 council pre-merge review (1 Critical + 4 Major)."*

A fresh contributor will assume "P2 council pre-merge review" means a council artifact under `icm/07_review/output/adr-audits/` similar to `0072-council-review-2026-05-04.md` or `0075-council-review-2026-05-04.md`. There is no such artifact for ADR 0076 P2; the council that produced these findings was COB's local pre-merge dispatch on the W#45 P2 implementation branch (per the resolved beacon at `icm/_state/research-inbox/_archive/cob-question-2026-05-04T21-22Z-w45-p2-adr-0076-a1.md`).

**Recommendation:** add a parenthetical: *"(council dispatched by COB on local `feat/w45-p2-blocks-crew-comms` branch; findings summarized in resolved beacon at `icm/_state/research-inbox/_archive/cob-question-2026-05-04T21-22Z-w45-p2-adr-0076-a1.md`; no separate council-artifact file under `icm/07_review/output/adr-audits/` because P2 implementation council was on the impl branch, not on this ADR)."*

---

## Perspective 2 — Pessimistic Risk Assessor (failure modes)

The transcript-hash is a security-relevant primitive: it is the input to the §Encryption handshake step 9 CONFIRM exchange, which is the only mechanism preventing a downgrade attack on `negotiatedCap` and the only mechanism binding the session to the same `tenantBytes` that both peers used in HELLO. A bug in the canonical form is silently exploitable until both implementations drift; then it surfaces as "spurious CONFIRM mismatch" (the failure mode the amendment is correctly trying to prevent) or — worse — as "matching CONFIRM hash on inputs that shouldn't match" (a collision-class attack the amendment does not consider).

### PR-1 (Major, → F2) — Missing domain-separator prefix; cohort drift from ADR 0046

The reduced form is:

```
SHA-256( ephemA[32] || idA[32] || ephemB[32] || idB[32]
       || uint32BE(len(tenantBytes)) || tenantBytes
       || negotiatedCap[1] )
```

This SHA-256 input is a raw concatenation of fixed-width fields + one length-prefixed variable-width field + one fixed-width field. There is **no domain-separator prefix** (e.g., `"sunfish-crew-comms-v1-transcript"` or a 1-byte protocol-version tag).

ADR 0046 §A2.3 (line 275 + 283) specifies the cohort canonical pattern for cryptographic input domain separation:

```
HKDF-SHA256(
  ikm  = sharedSecret,
  salt = "sunfish-encrypted-field-v1",
  ...
)
```

ADR 0076 itself (§step 7) uses the same pattern for HKDF: `salt = "sunfish-crew-comms-v1"`. The transcript-hash is a separate hash input over different bytes — but a future protocol revision (Phase 2, Phase 3 with audio, Phase 4 with video) that adds new fields to the handshake will produce a transcript-hash that is byte-equal to a same-prefix v1 transcript-hash if the new fields land at the end of the existing concat. Without a version tag in the hash input, the protocol cannot distinguish "v1 with no audio" from "v2 with audio negotiated to off" at the hash level. (At the wire level the `negotiatedCap` byte distinguishes them, but the transcript-hash is what binds CONFIRM.)

**Concrete failure mode:** in Phase 3 when `AUDIO_FRAME` ships and the handshake adds a new field (e.g., codec selection in ACCEPT), the transcript-hash input grows from 161 bytes → 162+ bytes. A v1-only peer and a v2 peer that downgrades to text-only will produce different transcript-hashes despite logically equivalent sessions, requiring a forced-CONFIRM-version-bump amendment. This is exactly the brittleness the amendment is trying to fix in the other direction.

**Recommendation:** prepend a 1-byte protocol-version tag (e.g., `0x01` for "crew-comms-v1") OR a domain-separator string (e.g., `"sunfish-crew-comms-v1-transcript\0"`) as the first field in the SHA-256 input. Cohort precedent: ADR 0046 + ADR 0076 §step 7 both use the `"sunfish-<package>-v<n>"` pattern. RFC 9180 (HPKE) `LabeledExtract` is the upstream authority — every subsequent crypto ADR from this cohort onward should use a labeled-input pattern unless explicitly waived. **Note:** this fix is a wire-incompatible change — but the W#45 P2 impl is HELD pending A1, so there is no deployed peer that has the un-labeled form. The cost to fix is one byte on every handshake.

### PR-2 (Minor, → F5) — Length-prefix width: `uint32BE` is overkill for tenant strings

`uint32BE(len(tenantBytes))` is 4 bytes for a tenant identifier whose realistic size is 1–64 bytes. A `uint8` (1 byte) or `uint16BE` (2 bytes) would carry the same information density without the 3-byte zero-padding. This is a minor efficiency point and harmless if oversize tenants are anticipated; but the amendment's prose claim *"The length-prefix on `tenantBytes` prevents extension attack via variable-length field adjacency"* is true regardless of width.

**Recommendation:** keep `uint32BE` if the author wants future-proofing for tenant-name-as-DN-string-style identifiers; otherwise document the choice. The transcript-hash is computed once per session so the byte cost is negligible — keeping `uint32BE` is the safer call.

### PR-3 (Minor, → F6) — Nomenclature drift in attack-class label

§A1 #1 says: *"The length-prefix on `tenantBytes` prevents extension attack via variable-length field adjacency."*

The standard cryptographic-attack-class names for the failure prevented by length-prefixing variable-width fields in a hash input are:
- "**ambiguous concatenation**" (most common in protocol-spec usage)
- "**canonicalization ambiguity**"
- "**domain confusion**" (when the variable-width field can swap with adjacent fixed-width fields)

"Length-extension attack" is a different concept — it specifically refers to Merkle-Damgård hash-function extension (e.g., `SHA-256(secret || data)` allowing an attacker to compute `SHA-256(secret || data || pad || extra)` without knowing `secret`). The transcript-hash here is not vulnerable to that even without length-prefix because the input has no secret prefix. So "extension attack" is mis-labeled.

**Recommendation:** replace *"prevents extension attack via variable-length field adjacency"* with *"prevents ambiguous concatenation (where a different `(tenantBytes, negotiatedCap)` split could produce the same byte sequence and thus the same hash)."*

### PR-4 (Major, → F4) — HEARTBEAT signable: `tenantBytes` length-prefix mismatch with transcript-hash

The amendment updates §HEARTBEAT row line 169:

```
signature = Ed25519(longTermPrivKey,
                   peerId[32] || UTF8(tenantId.Value) || caps[1] || timestamp[8 BE])
```

`UTF8(tenantId.Value)` here is **not** length-prefixed — but in the transcript-hash (line 173) the same logical field IS length-prefixed (`uint32BE(len(tenantBytes)) || tenantBytes`). The HEARTBEAT signable is therefore vulnerable to ambiguous-concatenation in the way the transcript-hash is not: an attacker who can choose `tenantId` and `peerId` values can construct two different `(peerId, tenantId)` pairs that produce the same `peerId[32] || UTF8(tenantId.Value)` byte sequence and thus the same Ed25519 signature input. Since `peerId` is fixed-width (32 bytes) this is **not actually exploitable** for HEARTBEAT — the boundary between `peerId` and `UTF8(tenantId.Value)` is unambiguous because the prefix is fixed-width. So this is a Major-grade *consistency* finding, not a Major-grade *exploitable* finding.

**However:** the HELLO signable in §step 2 has the same shape (`ephemeralPublicKey[32] || identityPublicKey[32] || UTF8(tenantId.Value)`) and the `UTF8(tenantId.Value)` is the LAST field — there is nothing after it, so length-ambiguity is also unexploitable. Same reasoning. So neither HELLO nor HEARTBEAT signable is broken — but the **inconsistency with the transcript-hash format is a maintenance trap**: a future amendment that adds a new field after `tenantBytes` to either signable (e.g., adding `displayName` to HEARTBEAT) needs to remember to length-prefix `tenantBytes` at that point or the signable becomes ambiguous. The amendment does not document this rule.

**Recommendation:** add a sentence to §A1 #2: *"Note: `UTF8(tenantId.Value)` is not length-prefixed in the HELLO and HEARTBEAT signables because it is the LAST variable-width field in those signables. Any future amendment that adds a field after `tenantId` in either signable MUST length-prefix `tenantId` at that time to preserve unambiguous concatenation."*

### PR-5 (Minor, → F7) — `tenantId` in HELLO signable: same logical field, different encoding

Per F4 reasoning the HELLO and HEARTBEAT signables omit the length-prefix because `UTF8(tenantId.Value)` is the trailing field. The transcript-hash includes the prefix because `tenantBytes` is followed by `negotiatedCap[1]`. This is internally consistent **once you reason through which field is last** — but it is not consistent at the prose level. A reader checking "the canonical form of `tenantId` on the wire" gets two different answers depending on which signable they look at.

**Recommendation:** add to §A1 #2 a single canonical statement: *"The wire encoding of `tenantId` is `UTF8(TenantId.Value)`. When `tenantId` appears in a hash or signature input as a non-trailing variable-width field, it MUST be preceded by `uint32BE(len(UTF8(tenantId.Value)))` (see transcript-hash for the pattern). When it appears as the trailing field, the length prefix is omitted."*

---

## Perspective 3 — Skeptical Implementer (verify every cited symbol)

Per ADR 0069 D3 three-direction discipline. Worktree base is `origin/main` @ `1e3d889`.

| Symbol cited in §A1 | Direction | Status |
|---|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | positive existence | ✓ verified at `packages/foundation/Assets/Common/TenantId.cs:8` — `public readonly record struct TenantId(string Value)` |
| `TenantId.Value` (string-backed) | structural | ✓ verified at TenantId.cs:8 — `string Value` |
| `PeerId.From(PrincipalId)` | positive existence | ✓ verified at `packages/federation-common/PeerId.cs:13` — exists |
| `PeerId.From(PrincipalId)` produces "raw 32-byte Ed25519 identity public key" | structural | ✗ FAIL — produces `PeerId(string Value)` where Value is the **base64url-encoded** form (43 chars). Raw bytes live on `PrincipalId.AsSpan()`. **F1 covers this.** |
| `PeerId(string Value)` | structural | ✓ verified at PeerId.cs:10 — `readonly record struct PeerId(string Value)` |
| `PrincipalId` 32-byte length | structural | ✓ verified at `packages/foundation/Crypto/PrincipalId.cs:31` — `public const int LengthInBytes = 32` |
| `identityPublicKey: bytes[32]` already in HELLO | positive existence | ✓ verified at ADR 0076 line 168 (HELLO row in message-type registry) — `identityPublicKey: bytes[32]` |
| `messageId` UUID encoding scope | positive existence | ✓ verified at ADR 0076 line 182 — UUID encoding mandate now scoped to `messageId` (DELIVERED 0x08, TEXT 0x10) |
| MessagePack key-ordering implementation-defined | structural | ✓ verified per MessagePack spec (https://github.com/msgpack/msgpack/blob/master/spec.md#serialization) — *"map keys MAY be in any order"* |
| Cohort precedent: HKDF salt `"sunfish-crew-comms-v1"` | positive existence | ✓ verified at ADR 0076 §step 7 line 198 (in worktree) — the same string |
| Cohort precedent: HKDF salt `"sunfish-encrypted-field-v1"` | positive existence | ✓ verified at `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md:275` |
| Cohort precedent: ADR 0028 §A7.8 CanonicalJson | positive existence | ✓ verified at `docs/adrs/0028-crdt-engine-selection.md:515,706,977,992,1095,1112` — `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` is the pinned canonical encoding for inter-peer JSON |
| ADR 0061 wire-format: PeerId base64url Ed25519 key | positive existence | ✓ verified at `docs/adrs/0061-three-tier-peer-transport.md:474,98` |

**Symbol verifications: 12 of 13 pass; 1 fails (F1).** Three-direction discipline catches the structural-citation failure that grep-only verification misses.

### SI-1 (Major, → F3) — No test vectors for transcript-hash or HEARTBEAT signable

The amendment ships pure prose; there are zero authoritative test vectors. The motivating concern for the amendment ("MessagePack key ordering is implementation-defined, meaning two conformant implementations may serialize identical logical payloads to different byte sequences") is exactly the kind of cross-implementation interop concern that test vectors are designed to lock down.

A future Sunfish session writing a second implementation (e.g., a `compat-zoom` adapter that needs to interoperate with the native `blocks-crew-comms` impl, or a Swift implementation for the iOS field app W#23) has no authoritative byte-level reference to verify against. The amendment's "the implementation already matched the reduced form" is operationally true today, but any second implementer will reverse-engineer the bytes from the prose and may drift on `uint32BE` byte-order, on `UTF8` BOM/no-BOM, on the boundary between `negotiatedCap[1]` and any future field, or (per F2) on the missing domain-separator.

**Cohort precedent:** RFC 8439 (ChaCha20-Poly1305) § Test Vectors. RFC 7748 (X25519) § Test Vectors. RFC 5869 (HKDF) § Test Vectors. Every IETF crypto RFC ships test vectors precisely because prose specifications are interop-fragile. ADR 0028 ships JSON canonical-form examples (the `FormFactorProfile` block at lines ~511–546) which serve the same function for that ADR's serialization.

**Recommendation:** add §A1 §"Test vectors" with at minimum:
- One worked transcript-hash: given `ephemA = 0x00..00`, `idA = 0x01..01`, `ephemB = 0x02..02`, `idB = 0x03..03`, `tenantBytes = "default"` (UTF-8), `negotiatedCap = 0x01`, the resulting SHA-256 hash bytes.
- One worked HEARTBEAT signable byte sequence (pre-Ed25519-sign): given `peerId = PrincipalId(0x04..04).AsSpan()`, `tenantBytes = "default"`, `caps = 0x07`, `timestamp = 1735689600` (`int64`, big-endian: `0x00 0x00 0x00 0x00 0x67 0x73 0xa3 0x80`), the concatenated 49-byte sequence.
- One worked HELLO signable byte sequence (pre-Ed25519-sign): given fixed `ephemeralPublicKey`, `identityPublicKey`, `tenantBytes = "default"`, the concatenated 71-byte sequence.

These vectors are mechanical to generate; they take the amendment from "prose claims" to "prose claims + falsifiable byte-level reference." A second implementer can verify their encoder produces the exact bytes; CI can lock down the canonical form against accidental drift.

### SI-2 (Mechanical, → F8) — Implementation checklist line tail "(A1)"

§Implementation checklist line 463 reads: *"…RFC 4122 big-endian UUID encoding for `messageId` fields only (not `Guid.ToByteArray()`); `tenantId` as raw UTF-8 bytes; `peerId` as raw bytes[32] Ed25519 key (A1)"*

The "(A1)" tail is unclear: is it a footnote pointing the reader at §A1, or is it a tag indicating the bullet was added in A1? Both are reasonable readings. Cohort convention varies.

**Recommendation:** rewrite as *"per §A1 (this amendment)"* or split the bullet so the A1-introduced changes are clearly demarcated.

---

## Perspective 4 — Devil's Advocate (was the right call made?)

Three decisions to challenge.

### DA-1 — Length-prefix vs domain-separator-string vs HKDF-with-info-string vs structured-CBOR

The amendment chose **length-prefix** for `tenantBytes` inside a raw-concatenation SHA-256 input. Alternatives:

| Option | Pros | Cons |
|---|---|---|
| Length-prefix (chosen) | Simple; no library dependency; prose-explainable; one byte per length-byte | No domain-separator (F2); inconsistent with HELLO/HEARTBEAT signable (F4/F7); no native test-vector convention |
| Domain-separator string + length-prefix | Adds version safety (F2 fix); cohort consistency with HKDF salt pattern | One additional 30-byte string; trivial cost |
| HKDF-with-info-string (use `HKDF-SHA256` instead of raw `SHA-256`) | RFC 5869; explicit domain separation via `info`; cohort consistency with §step 7; future-proof for re-using the transcript material as a key derivation input (Phase 3 audio session refresh?) | Slightly different semantics (KDF vs hash); HKDF output is by default 32 bytes which fits the `bytes[32]` slot |
| Structured-CBOR encoding | Self-describing; cross-platform interop is a solved problem with existing libs | New dependency (no CBOR lib in cohort yet); CBOR has its own canonicalization story (RFC 8949 §4); overkill for a 7-field transcript |

**Verdict:** length-prefix is the right primitive for this scope; CBOR is overkill; HKDF is interesting but solves a different problem (key derivation) than the immediate "give me a 32-byte fingerprint that both sides agree on" requirement. The right hybrid is **length-prefix + domain-separator string** (F2 recommendation). This is the canonical pattern for non-KDF hash binding in modern protocols (compare TLS 1.3 transcript-hash, which uses `Hash(Hash(ClientHello..ServerFinished) || "tls13 derived" || …)` for explicit domain separation).

### DA-2 — UTF-8 bytes vs UUID-coerced TenantId

The amendment correctly identifies that forcing UUID encoding would require redefining `TenantId` across every multi-tenant package (Foundation, federation, blocks-*). The cost is enormous; the benefit (uniform UUID semantics on the wire) is purely aesthetic.

**Counter-arguments considered:**
- *"UUIDs sort cleanly; UTF-8 strings can have collation issues."* — Tenant identifiers are not sorted in this protocol; they are byte-compared for equality (`sender.tenantId == local.tenantId`) which is collation-free.
- *"UUIDs are fixed-width; strings are variable-width."* — Length-prefix solves this where variable-width is a problem (transcript-hash). Where it's not a problem (trailing field in signable), no fix is needed.
- *"UUIDs are opaque; strings can leak organizational structure."* — Real-world tenant IDs in this cohort are often human-readable (`"acme-corp-prod"`); the leak is a feature, not a bug, for operator diagnostics.

**Verdict:** UTF-8 bytes is the right call. Decision is sound.

### DA-3 — `bytes[32]` raw key vs UUID-coerced PeerId

The amendment correctly identifies that `PeerId` is conceptually a public key (Ed25519, 32 bytes), and `identityPublicKey` already appears as `bytes[32]` in HELLO. The HEARTBEAT signature is computed over the public-key bytes (so that the receiver can verify with the same public key it loads from the roster). Using `bytes[32]` for `peerId` on the wire aligns the two fields and removes a serialization step.

**Counter-arguments considered:**
- *"UUID encoding gives consistent fixed-width across all ID fields."* — Conceptually clean, but `peerId` is not a UUID. Coercing it into the UUID slot just to get fixed-width is form over function.
- *"Cross-platform UUID handling is well-tooled."* — True, but `bytes[32]` is even simpler — there is no UUID byte-order trap (RFC 4122 mixed-endian) to land on.

**Verdict:** `bytes[32]` is the right call. Decision is sound. (But F1 — the prose miscites how to recover those bytes from `PeerId.From(PrincipalId)`. That is an implementation-correctness footgun, not a decision-correctness defect.)

### DA-4 — Compatibility with Loro / YDotNet handshake patterns elsewhere in cohort

ADR 0028 (CRDT engine selection) does NOT define a peer-to-peer handshake protocol — sync is mediated by `ISyncTransport` whose envelope-level protocol is specified at ADR 0061 + ADR 0070-style sync-message protocols, not at the CRDT-engine layer. There is no Loro/YDotNet handshake to be compatible with at this layer; `foundation-channels` is a sibling to `blocks-messaging` (per ADR 0052), not a consumer of CRDT handshakes.

**Verdict:** no Loro/YDotNet compatibility concern; the question is moot. (If a future ADR layers CRDT sync on top of `IChannelSession` for live document collaboration, that future ADR will need to reconcile its own handshake with this transcript-hash; not a today-problem.)

---

## UPF v1.2 Stage 2 anti-pattern scan (21 patterns)

| AP | Status | Notes |
|---|---|---|
| AP-1 (unvalidated assumptions) | ⚠ partial | The "implementation already matched the reduced form" claim is unverifiable on origin/main (W#45 P2 branch is local on COB's machine; not pushed). The amendment text accepts this as fact. Future verification gate: when COB pushes `feat/w45-p2-blocks-crew-comms`, post-push spot-check should confirm. Not a blocker since the amendment's correctness does not depend on this claim — the reduced form is the right design regardless of whether the impl already matched. |
| AP-2 (vague phases) | ✓ clean | Three numbered decisions; each scoped. |
| AP-3 (vague success criteria) | ✓ clean | "Implementation already matched" is a falsifiable success claim once P2 ships. |
| AP-4 (no rollback) | ✓ clean | Spec-only amendment; rollback = revert PR #548. |
| AP-5 (plan ending at deploy) | n/a | Not a plan ADR. |
| AP-6 (missing Resume Protocol) | n/a | Single-session amendment authoring. |
| AP-7 (delegation without contracts) | ✓ clean | COB hand-off via beacon resolution. |
| AP-8 (blind delegation trust) | ⚠ — | The amendment trusts COB's pre-merge council finding labels ("1 Critical + 4 Major") without re-deriving them. F9 covers provenance attribution. |
| AP-9 (skipping Stage 0) | ✓ clean | The choice between length-prefix / domain-separator / HKDF / CBOR is implicit (DA-1). Author would benefit from making it explicit, but the chosen path is defensible. |
| AP-10 (first idea unchallenged) | ⚠ — | F2 (missing domain-separator) is exactly the unchallenged-first-idea defect. |
| AP-11 (zombie projects / no kill) | n/a | Not an open-ended initiative. |
| AP-12 (timeline fantasy) | n/a | Not a timeline-bearing amendment. |
| AP-13 (confidence without evidence on crypto claims) | ⚠ — | "The length-prefix on `tenantBytes` prevents extension attack" — the *claim* is correct (the prefix prevents ambiguous concatenation) but the *evidence* and the *attack-class label* are not given (F6). For crypto claims this matters; "extension attack" is a specific named vulnerability and the amendment is using it loosely. |
| AP-14 (wrong detail distribution) | ✓ clean | Detail concentrated on the change; baseline §Wire protocol untouched where unchanged. |
| AP-15 (premature precision) | n/a | Wire-encoding stability is the explicit goal; precision is appropriate. |
| AP-16 (hallucinated effort estimates) | n/a | No effort estimate. |
| AP-17 (delegation w/o context transfer) | ✓ clean | §A1 prose is sufficient context for COB to rebase the held branch; F1/F3 fixes would tighten this further. |
| AP-18 (unverifiable gates) | ⚠ — | Without test vectors (F3), the gate "implementations agree on transcript-hash bytes" is effectively unverifiable cross-implementation. |
| AP-19 (missing tool fallbacks) | n/a | Spec amendment, no tooling. |
| AP-20 (discovery amnesia) | ⚠ — | Cohort precedent on HKDF-with-info-string (ADR 0046, ADR 0076 §step 7) is not surfaced in the rationale. F2 covers this. |
| AP-21 (assumed facts without sources / cited-symbol drift) | ✗ FAIL | F1 — `PeerId.From(PrincipalId)` mis-citation. Three-direction discipline caught what grep-only would miss. |

**Anti-pattern scan: 16 clean / 5 partial-or-warning / 1 fail.** AP-21 fail is F1. AP-13/AP-18/AP-20 partials map to F2/F3/F6.

---

## Crypto-specific spot-checks

Per task brief — wire-encoding errors here are security-relevant.

| Check | Status | Notes |
|---|---|---|
| Hash function identified | ✓ | SHA-256 named in transcript-hash + UUID encoding (RFC 4122 not a hash; SHA-256 is the only cryptographic hash in §A1). Cohort default per ADR 0028 / ADR 0046. |
| Endianness explicit on multi-byte fields | ⚠ partial | `uint32BE` ✓ on transcript-hash length-prefix. `timestamp[8 BE]` ✓ added to HEARTBEAT signable. But `caps[1]` and `negotiatedCap[1]` are 1-byte so endianness is moot ✓. The `int64` timestamp annotation only appears in the HEARTBEAT signable line; the HEARTBEAT row in the message-type registry says `timestamp: int64` without BE/LE annotation — interop trap if a non-.NET implementer assumes native byte order. **Note:** this is a baseline-ADR defect not introduced by A1, but A1 is the right place to fix it since the related rows are being touched. |
| Length-prefix width specified | ✓ | `uint32BE(len(tenantBytes))` is unambiguous. F5 challenges the *width choice* (4 bytes oversize for tenant strings), not the specification. |
| Domain-separation tag | ✗ FAIL | Not present (F2). Cohort drift from ADR 0046. |
| Test vectors present | ✗ FAIL | Not present (F3). RFC convention for crypto specs. |
| Concatenation collision risk | ✓ on transcript-hash | Length-prefix on `tenantBytes` removes the only variable-width-adjacency concern. F4 documents the maintenance trap on HELLO/HEARTBEAT signables but those are unexploitable today. |
| Backward-compat with pre-A1 wire bytes | ✓ | No deployed peer; W#45 P2 is HELD pending A1; the impl already matches A1's reduced form per beacon. F2's recommendation is wire-incompatible but only against not-yet-shipped impl, so cost is one re-build. |
| Cross-platform endianness on uint32 wire-order | ✓ | `uint32BE` is unambiguous network byte order. |
| `Guid.ToByteArray()` mixed-endian trap documented | ✓ | §Wire protocol UUID/GUID encoding paragraph (line 182) covers this; A1 correctly scopes the mandate to `messageId` only. |
| `peerId` raw-key derivation path | ⚠ — | F1 — the rationale claims `PeerId.From(PrincipalId)` produces raw bytes; it produces a base64url string. The implementation has to base64url-decode at encode time OR carry `PrincipalId` alongside `PeerId`. |

**Crypto spot-check: 8 pass / 2 fail / 2 partial.** The two fails (F2 domain-separator + F3 test vectors) are the ones that future cross-implementation interop hinges on.

---

## Recommendations to author

In priority order:

1. **F1 (Major)** — Rewrite §A1 #3 second sentence to correctly describe `PeerId.From(PrincipalId)`'s output (string-typed `PeerId.Value` is base64url-encoded; raw bytes live on `PrincipalId.AsSpan()`; encoder either carries `PrincipalId` or base64url-decodes at wire-encode time). Cross-reference ADR 0061 line 474 if helpful.

2. **F2 (Major)** — Add a domain-separator prefix to the transcript-hash input. Recommended: prepend `"sunfish-crew-comms-v1-transcript\0"` UTF-8 bytes (or similar). Cohort precedent: ADR 0046 / ADR 0076 §step 7 / TLS 1.3 / HPKE `LabeledExtract`. This is wire-incompatible against the current local impl but only by 32+ bytes added to the hash input — recompile + retest.

3. **F3 (Major)** — Add §A1 §"Test vectors" with at minimum one worked transcript-hash, one worked HEARTBEAT signable byte sequence, and one worked HELLO signable byte sequence. Cohort precedent: every IETF crypto RFC. Without these the cross-implementation interop concern that motivates A1 is unfalsifiable.

4. **F4 (Major)** — Add a sentence to §A1 #2 documenting the length-prefix rule for `tenantBytes` in hash/signature inputs: the prefix is omitted only when `tenantBytes` is the trailing variable-width field; any future signable that adds a field after `tenantBytes` MUST length-prefix it.

5. **F5 (Minor)** — Optional: justify or right-size the `uint32BE` length-prefix width (1, 2, or 4 bytes — the choice of 4 is over-provisioned for realistic tenant identifiers but harmless).

6. **F6 (Minor)** — Replace "extension attack via variable-length field adjacency" with "ambiguous concatenation" or "canonicalization ambiguity." "Extension attack" is the named class for Merkle-Damgård length-extension and is the wrong label.

7. **F7 (Minor)** — Pin the canonical statement of `tenantId` wire encoding (UTF8(TenantId.Value)) in one place in §A1; explicitly call out that the length-prefix appears only when `tenantId` is non-trailing in a hash/signature input.

8. **F8 (Mechanical)** — Implementation checklist line 463 tail "(A1)" → rewrite as "per §A1 (this amendment)."

9. **F9 (Mechanical)** — Add a parenthetical attributing the "1 Critical + 4 Major" finding labels to COB's local pre-merge council on the W#45 P2 implementation branch (not a council on this ADR itself); cite the resolved beacon path.

If F1–F4 are applied, this becomes a clean A1. If only F1 is applied, the amendment is shippable but cohort drift on F2 + F3 will need a follow-on amendment when Phase 3 audio lands or when a second implementation is written.

---

## Cohort batting impact

- **Before this review:** 27-of-28 substrate amendments needed council fixes (96.4%); ADR 0028 A11 is the only PASS-on-first-try.
- **If author applies F1–F9 to A1:** A1 lands as the **28th** case where pre-merge council was canonical (28-of-29; 96.6%). Pattern unchanged.
- **If author defends decisions and ships as-is:** A1 ships with one structural-citation defect (F1) and three crypto-correctness gaps (F2, F3, F4 documentation rule). Cohort metric records as "shipped without fixes" — but the F1 cited-symbol drift is the kind of defect that surfaces operationally in implementer follow-on PRs, so the cohort batting average would still effectively reflect the fix being made downstream.

The amendment is **good substrate work** — the three decisions are correct and the rationale is clear. The findings are about **rationale precision** and **cohort-canonical hygiene** (test vectors + domain separation), not about whether the amendment is the right answer. Same shape as ~80% of the cohort's prior-amendment findings: small mistakes in citation, missing-test-vector pattern, drift from cohort-canonical primitives.

---

## Process notes

- **Effort:** Opus 4.7, `xhigh` (per CO directive 2026-04-30 + ADR 0069 D1 canonical effort for pre-merge council).
- **Worktree base:** `chore/icm-0076-a1-council` from `origin/main` @ `1e3d889`. Did not touch `docs/adr-0076-a1-wire-encoding`.
- **Three-direction discipline (ADR 0069 D3) applied:** verified positive existence of every cited symbol on origin/main; verified structural shape of `PeerId`, `PrincipalId`, `TenantId` types; caught F1 (cited-symbol structural drift on `PeerId.From(PrincipalId)`).
- **Crypto-correctness emphasis:** spot-check ran 10 crypto-specific checks (hash function / endianness / length-prefix width / domain-separation / test vectors / collision risk / backward-compat / wire-order / Guid mixed-endian / raw-key derivation). 2 fails (F2, F3); 2 partials.
- **No mechanical fixes applied** per task constraints.

---

**Review completed 2026-05-04.** Awaiting author response.
