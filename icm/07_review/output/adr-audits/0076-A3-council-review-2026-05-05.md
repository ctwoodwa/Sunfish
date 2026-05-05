# ADR 0076 Amendment A3 Council Review — Pre-merge Canonical (Stage 1.5)

**ADR / Amendment:** [ADR 0076 §A3 — Conformance test vectors (HELLO / HEARTBEAT / CONFIRM transcript)](../../../../docs/adrs/0076-crew-comms-foundation-channels.md)
**PR under review:** [#600](https://github.com/ctwoodwa/Sunfish/pull/600) — `docs(adrs): 0076 Amendment A3 — conformance test vectors (HELLO / HEARTBEAT / transcript-hash)`
**Branch under review:** `docs/adr-0076-a3-test-vectors` (HEAD `2e49aad`; `mergeable: MERGEABLE`)
**Reviewer:** XO research session (Opus 4.7, `xhigh`)
**Date:** 2026-05-05
**Discipline:** ADR 0069 D1 (pre-merge council canonical for behavior-bearing artifacts), D2 (§A0 self-audit pressure-test), D3 (three-direction structural-citation spot-check); crypto-correctness pressure-test commensurate with the interop-falsifiability load A3 carries.
**Worktree base:** `origin/main` (post `chore(inbox): pao status` merge — `a08ae4e`)
**A3 worktree:** `/private/tmp/sunfish-0076-a3` @ `2e49aad`
**Cohort context (global):** 29-of-30 substrate amendments needed council fixes (~96.7%); ADR 0028 A11 was the cohort's only PASS-on-first-try.
**Cohort context (W#45 substrate):** 24-of-24 P-phase substrate-impl reviews surfaced fixes (P2: 1 Critical + 4 Major; P4: 2 Critical + 8 Major).
**Crypto-correctness emphasis:** A3 vectors define the **canonical wire-encoding of every Ed25519 signature input and every CONFIRM SHA-256 input** for crew-comms. A defect that ships here propagates as silent interop failure on every future second-language implementation (W#23 iOS, eventual Kotlin/Rust ports). Spot-check pressure-test extended to full hand-recompute of three vectors.

---

## TL;DR / Verdict

**PASS** — with **two Minor findings** flagged for follow-up. The amendment as authored is mergeable as-is; the two Minor findings are non-blocking and either (a) pre-existing on `main` (M2) or (b) defensible engineering trade-offs that the amendment text already discloses (M1). One **Mechanical** finding (M3) on a self-reported byte-count off-by-21 in the script's stdout message is cosmetic and need not block.

A3 is the cohort's **second** counter-example to the pre-merge-fixes-needed pattern, alongside ADR 0028 A11 (W#42 Sequestration feature-gate-off amendment). Both pass-on-first-try cases share three properties: (1) small surface area, (2) zero new `Sunfish.*` types introduced, (3) author had a fully verifiable check available at authoring time (here, two consecutive `--check` runs and three independent hand-computations).

**Findings summary:** 0 Blocking + 0 Major + 2 Minor + 1 Mechanical.

| Class | Count | IDs |
|---|---|---|
| **Blocking** | 0 | — |
| **Major** | 0 | — |
| **Minor** | 2 | M1 (Unicode-normalization gap on `TenantId.Value` UTF-8 encoding), M2 (pre-existing broken §A2.8 reference to non-existent `0076-a2-council-review-2026-05-05.md`) |
| **Mechanical** | 1 | M3 (script `print(f"… {len(rendered)} bytes …")` reports 11,980 — actually counts UTF-8 *characters*; the file is 12,001 bytes on disk because of multi-byte UTF-8 in V3/V9 tenants) |

**Cohort batting-average update:** **30-of-31** (96.8%); A3 joins ADR 0028 A11 as the cohort's two PASS-on-first-try substrate amendments. (Note: the parent prompt's reference figure 29-of-30 already matches the post-A1+A2 state on this ADR; A3 is the 31st cohort amendment to receive pre-merge council. PASS.)

**Domain-separator deferral disposition:** **A3 should ship as-is — DO NOT include domain-separator now.** F2 is a forward-compatibility property (collision-resistance with a hypothetical v2 protocol that does not exist on the roadmap). F3 is an immediate-blocker property (every second-language implementer is blocked on prose-only spec right now). The §A3 trade-off (close F3 now; accept ONE regeneration of V7/V8/V9 when F2 ships) is correct engineering. The §A3.5 "Update path" prose explicitly walks the regeneration sequence, which discharges the cohort's "interop-falsifiability + future-cost-disclosure" obligation. Shipping vectors *with* a speculative domain-separator now would lock in a string ("sunfish-crew-comms-v1-transcript" or whatever) that a future F2 amendment would still need to ratify on its own merits — and the F2 amendment would then either (a) ratify the speculative string and waste no work, or (b) want a different string and force a re-spec amendment plus regeneration anyway. The actual engineering loss from "ship now without separator" is bounded at 3 hex strings and 3 SHA-256 hashes; the engineering loss from "guess at the separator now" is potentially the same plus a worse v2 design constraint.

---

## Spot-check results — three independent hand-computations

I ran the generator's logic from scratch in a cleanroom Python session (`python3 -c …`) using only `hashlib`, `struct`, and `cryptography.hazmat.primitives.asymmetric.ed25519` / `x25519` — not by importing the generator. I derived all four keypairs from the SHA-256 phrase rule, assembled signables/inputs by hand, and compared the resulting hex strings byte-for-byte against the JSON artifact.

### V1 (HELLO; ASCII tenant `tenant-001-acme`; presence.caps=0x07)

| Field | Hand-computed | JSON artifact | Match |
|---|---|---|---|
| `signable` | `005f111c8869fa00…fb8e07911c91…74656e616e742d3030312d61636d6507` | (V1 `expected_signable_hex`) | **identical** |
| `signable.length` | 84 | 84 | ✓ |
| `signature` | `b185c034c93a312a670fb46ae30b4818f5bde417fdb3e0695d6c6aa7cb94128b51ff8bc03c75244ad4d28db3ee0828882c03eb6fec86246bbabfab55bd3b4101` | (V1 `expected_signature_hex`) | **identical** |

Length-math cross-check (per §A1.4 row 0x01): `32 + 32 + 4 + 15 + 1 = 84` ✓. Tenant byte sequence is the ASCII of `tenant-001-acme` (`74656e616e742d3030312d61636d65` = 15 bytes). Length prefix `0000000f` = `uint32BE(15)` ✓.

### V5 (HEARTBEAT; 63-byte ASCII tenant `xxx…x`; caps=0x07; ts=1735689600000)

| Field | Hand-computed | JSON artifact | Match |
|---|---|---|---|
| `signable` | `01f61a817230f3ab…b6cd50a 0000003f 78×63 07 000001941f297c00` | (V5 `expected_signable_hex`) | **identical** |
| `signable.length` | 108 | 108 | ✓ |
| `signature` | `659265c63a6112faf1b9f19fbb79a49ac0cb5bb482a2fb30a1e13c5603de26f2fe1fc3cfbb8acbebc7f4960e6caf7bf22754992888f3050666b5b6009884dd0e` | (V5 `expected_signature_hex`) | **identical** |

Length-math cross-check (per §A1.4 row 0x02 + §A1.3 §A3): `32 + 4 + 63 + 1 + 8 = 108` ✓. Length prefix `0000003f` = `uint32BE(63)` ✓ (this is the exact byte the council scope flagged as the length-prefix-endianness sentinel: `0x3F` if BE-correct, `0x3F00` if LE-confused). Timestamp big-endian `000001941f297c00` decodes to `1735689600000` ✓ (= `2025-01-01T00:00:00Z` UTC). PeerId = `resp_pub` raw 32 bytes (NOT base64url-string-of-pubkey), confirming §A1.3 §A3 mandate.

### V7 (CONFIRM; ASCII tenant `tenant-001-acme`; A1+A2 ratified form)

| Field | Hand-computed | JSON artifact | Match |
|---|---|---|---|
| `input` | `… 0000000f 74656e616e742d3030312d61636d65 07 01 07 03` | (V7 `expected_input_hex`) | **identical** |
| `input.length` | 151 | 151 | ✓ |
| `sha256` | `5c38292a921ea0bff9a3b20b49e255d8e8eb06579e1aaa44aa11ad539f03a8fb` | (V7 `expected_sha256_hex`) | **identical** |

Length-math cross-check (per §A2.3 §A1ext): `4 × 32 + 4 + 15 + 4 × 1 = 151` ✓. Field-ordering cross-check: trailing 4 capability bytes are `inviteCaps[1] || negotiatedCap[1] || presenceCapsA[1] || presenceCapsB[1]` = `07 01 07 03` ✓ — this is the **A2-ratified order** (A2 inserted `inviteCaps[1]` before `negotiatedCap[1]`), NOT the A1.5 form. The A2 supersession is correctly applied throughout the generator (`confirm_transcript_input` line 199-210 of the script).

**All three hand-computations agree byte-for-byte with the committed JSON.** No halt-condition triggered.

### Determinism check

```
$ python3 tools/icm/generate-channel-vectors.py --check
channel-test-vectors.json is up-to-date.
$ python3 tools/icm/generate-channel-vectors.py
wrote tools/icm/channel-test-vectors.json (11980 bytes; 9 vectors)
$ python3 tools/icm/generate-channel-vectors.py --check
channel-test-vectors.json is up-to-date.
```

Two consecutive runs produced byte-identical JSON. `--check` exits 0. Determinism is established. (One nit: the `len(rendered)` count emitted at re-generation time is 11,980 *characters*, not 11,980 *bytes*; on disk the file is 12,001 bytes because `é` / `ü` / `ï` / `ö` / `ë` and the em-dash in the rule string each cost 2 UTF-8 bytes. See M3.)

### Ed25519 determinism cross-impl

Independent confirmation that PyCA `cryptography` Ed25519 produces identical signatures across N=5 same-input runs (RFC 8032 mandates deterministic Ed25519; no nonce/IV randomness):

```
$ python3 -c "from cryptography… ; sk = …seed; print(set(sk.sign(b'hello').hex() for _ in range(5)))"
{'f072f3a2e5b31d7fd2978b92e6a620fd…'}   # set has size 1 — 5 identical sigs
```

NSec.Cryptography (the .NET implementation A3 vectors will be consumed against in W#45 P4.5 PR 1) also implements RFC 8032 Ed25519 (libsodium-backed), so the cross-impl identity claim of the §A3.4 conformance protocol holds by construction. The same identity holds for swift-crypto (Apple, BoringSSL-backed) for the eventual W#23 iOS path.

---

## §1 — Crypto / security correctness (focus areas 1–5)

### §1.1 Domain-separator deferral disposition (focus area #1) — PASS

The amendment defers F2 (no domain-separator prefix on transcript-hash) to a future amendment, citing F3 as the immediate-blocker priority. Three considerations:

1. **F2 is a forward-compatibility concern, not a present-day vulnerability.** The current SHA-256 input is fixed-width up to one length-prefixed variable field, then all-fixed-width again. There is no length-extension attack on SHA-256 itself; there is no hash-collision attack on the input domain because every field's position is determined by either the fixed structural offset or the explicit `uint32BE` length prefix. The "domain separator" hardening is only meaningful when **a future v2 protocol redefines the input concatenation**, at which point a same-bytes-different-meaning collision becomes possible. v2 is not on the W#45 / W#23 / W#46 roadmap.

2. **F3 is an immediate, ongoing blocker for cross-impl interop.** W#45 P4.5 PR 1 (the .NET catch-up to A1+A2) needs known-answer fixtures right now; W#23 iOS Swift impl will need the same fixtures imminently. The two impls cannot validate against prose alone.

3. **The cost of "ship now, regenerate later" is bounded and disclosed.** §A3.5 "Update path" walks the regeneration sequence in 4 numbered steps. The HELLO and HEARTBEAT vectors (V1–V6) are unaffected by F2; only V7/V8/V9 expected outputs would change. The amendment commits to that update path explicitly.

**Adversarial counter-position considered:** "Vectors should reflect the FINAL canonical form, not pre-F2." Rejected because (a) the final form is not yet specified — F2's amendment hasn't been authored, the separator string isn't chosen, the hash construction (HKDF? simple prefix? versioned binary tag?) isn't decided; (b) shipping speculative F2 vectors now creates a self-fulfilling-prophecy commitment to whatever the speculative spec was, weakening the F2 amendment's design freedom; (c) the amendment's prose explicitly marks itself as "pre-F2" via the `"ratified_form": "A1+A2 (no domain-separator; F2 deferred to a later amendment)"` field in the JSON header — there is no risk of an implementer mistaking these as F2-final.

**Disposition:** PASS. The deferral is correct engineering. The §A3.5 update-path prose discharges the cohort's "future-cost-disclosure" obligation. **No amendment required.**

### §1.2 Test-keypair provenance (focus area #2) — PASS

Seeds are SHA-256 of canonical phrases (`"sunfish-channels-test-initiator-id-v1"` etc.). Resolved seeds are non-degenerate (e.g., `1ad454587580…` for the initiator identity — not all-zeros, not a low-bit value, no obvious algebraic structure). Per RFC 8032 §5.1.5 (Ed25519 key generation from a 32-byte seed) and RFC 7748 §6.1 (X25519 from a 32-byte seed), any conforming implementation derives identical public keys + identical signatures from identical seed bytes.

The council scope's hypothetical "all-zeros seed" concern was flagged as a worth-checking idiom; A3 does not use all-zeros (which would, in fact, be valid — Ed25519 over an all-zero seed produces a defined and deterministic public key — but it is bad fixture hygiene because a developer might mistake the test pubkey for a "zero / not-yet-set / sentinel" value). The phrase-derived seed approach is superior to all-zeros: the seeds are visibly random-looking, the derivation rule is fully reproducible from source, and the "MUST NEVER be used in production" warning in `seed_provenance.warning` is verifiable by any auditor.

**Disposition:** PASS. Provenance is robust + reproducible.

### §1.3 Length-math cross-checks (focus area #3) — PASS

Hand-recomputed the three vectors flagged in the council scope:

- **V1 HELLO 84-byte signable:** `32 + 32 + 4 + 15 + 1 = 84` ✓ (tenant = `tenant-001-acme` = 15 UTF-8 bytes; presence.caps trailing 1 byte)
- **V7 CONFIRM 151-byte input:** `4 × 32 + 4 + 15 + 4 = 151` ✓ (4 keys + length prefix + tenant + 4 capability bytes)
- **V5 HEARTBEAT 108-byte signable:** `32 + 4 + 63 + 1 + 8 = 108` ✓ (peer + length prefix + 63-byte tenant + caps + ts)

Additionally cross-checked **all 9 vectors** programmatically: every `expected_*_length` field equals `len(expected_*_hex) // 2`. No mismatches. (See `python3 -c …` block in spot-check section.)

The length-math formulas in §A3.6 §A0.3 are correct — but I noticed the formula text reads `"32 + 32 + 32 + 32 + 4 + len(tenantBytes) + 1 + 1 + 1 = 135 + len"` for the §A1.3 §A1 form, then notes the §A2.3 §A1ext "+1 = 136 + len(tenantBytes)" form. This is correct (A2 added one byte for `inviteCaps`) and matches V7/V8/V9 lengths.

**Disposition:** PASS. Length-math is rigorous and trace-able.

### §1.4 Edge-case coverage (focus area #4) — Minor finding M1 only

The 9-vector coverage matrix exercises:
- **Length-prefix endianness** — V5 has `0x0000003f` length byte where a LE-confused implementation would write `0x3f000000` ✓
- **Zero-length tenant** — V2 + V8 ✓
- **Boundary 1-byte tenant** — V6 ✓
- **Max-practical 63-byte tenant** — V5 ✓
- **UTF-8 multi-byte tenant** — V3 + V9 (`tenant-é-ünïcödë` = 21 UTF-8 bytes vs 17 logical chars) ✓
- **Asymmetric capability bitmasks** — V9 (`presA=0x07, presB=0x06`) ✓
- **All-bits-set capability** — V1, V5 (`0x07`) ✓; V9 (V9's negotiatedCap=0x02 isolated) ✓

**Not covered:**

#### **M1 (Minor) — Unicode-normalization gap on `TenantId.Value`**

V3 + V9 use `tenant-é-ünïcödë` which Python source files default to **NFC** (precomposed). A producer constructing `TenantId("tenant-é-ünïcödë")` from a different source (e.g., file system input on macOS where HFS+ stores filenames in NFD; some database-driver encodings) would produce **decomposed UTF-8 bytes** for the same logical string, e.g., `é` = `e` + `U+0301 (combining acute)` instead of the precomposed `U+00E9`. The decomposed byte sequence is `74656e616e742d65cc812d75cc88…` (extra 2 bytes per accented char), not the `74656e616e742dc3a92dc3bc…` the vector locks in.

The amendment's wire-encoding spec (§A1.3 §A2) says: *"`tenantBytes = System.Text.Encoding.UTF8.GetBytes(TenantId.Value)`"* — but does **not** state which Unicode normalization form `TenantId.Value` is in, nor mandate normalization at construction time. A fresh contributor (or a non-.NET impl) could legitimately produce divergent bytes for the same logical string and silently fail interop.

**Recommendation (deferrable; non-blocking for A3):** the F2-companion amendment (or a follow-up A4 tightening) should add either:
- *(Option a)* "TenantId.Value MUST be in Unicode NFC. Producers MUST `String.Normalize(NormalizationForm.FormC)` at construction time" — and add NFD vectors that are documented as INVALID inputs producers should reject; OR
- *(Option b)* "TenantId.Value is treated as opaque bytes; producers are responsible for ensuring consistent encoding across all roster members. Cross-tenant `TenantId` equality is byte-equality of `Value`, not Unicode-equivalence."

The council scope flagged this as a sub-concern of focus area #4, and I think it is real — but it does not block A3. A3 ships *what it ships*: a vector set that locks in the NFC byte sequence for `tenant-é-ünïcödë`. If a future implementer feeds NFD bytes for the same logical tenant, the vectors fail loudly (which is the desired interop-falsifiability behavior). The amendment text should be tightened in a follow-up to mandate NFC at the source, but the current vectors are correct.

Also not covered (and out-of-scope for this amendment, per stated limits):

- **Pathological UTF-8** (combining marks, RTL, surrogates) — out of scope per author's stated coverage axes; would belong in a follow-up A4.
- **Long peerId / max capabilities** — `peerId` is fixed-width 32 bytes (raw Ed25519 pubkey); no length variation possible. `negotiatedCap` is 1 byte (max 255 distinct values); the `cap_text|cap_audio|cap_video = 0x07` case in V1+V5 covers all-bits-set for the v1 ChannelCapability enum.
- **Negative cases (signature mismatch / "this is what failure looks like")** — out of scope per cohort precedent (RFC 8439, RFC 7748, RFC 5869 all ship positive vectors only; negative testing is the consumer's responsibility). The spec's failure modes are documented in the conformance-protocol prose (§A3.4 Check 1/2/3) which says how to *interpret* a mismatch; the vectors themselves don't need negative cases.

### §1.5 CONFIRM "full A1+A2 form" (focus area #5) — PASS

V7/V8/V9 use the A2-superseded form: `… || tenantBytes || inviteCaps[1] || negotiatedCap[1] || presenceCapsA[1] || presenceCapsB[1]`. Cross-verified against:
- **§A2.3 §A1ext** (canonical text) — order matches ✓
- **§A2.4 wire protocol table** (CONFIRM row 0x0A) — order matches ✓
- **§A2.5 step 9** (handshake step) — order matches ✓
- **`origin/main` ADR text** (post PR #566) — order matches; A1+A2 prose on branch is byte-identical to main except for the +A3 frontmatter entry and the +A3 appendix ✓
- **`confirm_transcript_input(…)` in `tools/icm/generate-channel-vectors.py` lines 169–210** — order matches ✓

The generator's implementation explicitly comments: `"|| inviteCaps[1]              -- A2 addition"` and follows with `negotiatedCap[1]`, `presenceCapsA[1]`, `presenceCapsB[1]`. The author's mental model and the code are aligned.

V7's expected hex tail: `0000000f 74656e616e742d3030312d61636d65 07 01 07 03` — that's `len=15 || "tenant-001-acme" || inviteCaps=0x07 || negotiatedCap=0x01 || presenceCapsA=0x07 || presenceCapsB=0x03` — which exactly matches the council scope's spot-check hypothesis.

**Disposition:** PASS. A2-ratified field-order is correctly applied throughout the generator and JSON.

---

## §2 — Cited-symbol verification (focus areas 6–10)

Three-direction structural-citation discipline (positive existence + negative existence + structural correctness) per ADR 0069 D3.

### §2.1 `PrincipalId.AsSpan()` at PrincipalId.cs:49 — VERIFIED

```csharp
// packages/foundation/Crypto/PrincipalId.cs
49:    public ReadOnlySpan<byte> AsSpan() => _bytes;
```

`_bytes` is the 32-byte raw Ed25519 public key (per `LengthInBytes = 32` at line 31 + the `FromBytes` ctor enforcement at line 41-46 + the public-key ingestion in `KeyPair.cs` line 19-21: `var publicBlob = _key.PublicKey.Export(KeyBlobFormat.RawPublicKey); _principalId = PrincipalId.FromBytes(publicBlob);`). Cited line + signature exact ✓.

### §2.2 `KeyPair.Sign` at KeyPair.cs:54 — VERIFIED

```csharp
// packages/foundation/Crypto/KeyPair.cs
54:    public Signature Sign(ReadOnlySpan<byte> data)
```

Returns NSec `Signature` (64 bytes Ed25519 signature). The XML doc at lines 41-52 explicitly documents this is the protocol-level signing path used by crew-comms HELLO/HEARTBEAT (vs the canonical-JSON `Ed25519Signer` path used by ledger envelopes). The §A3.4 Check 2 cites exactly this method. ✓

### §2.3 `KeyPair.VerifyRaw` at KeyPair.cs:67 — VERIFIED

```csharp
// packages/foundation/Crypto/KeyPair.cs
67:    public static bool VerifyRaw(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
```

Static; takes raw 32-byte public key + data + 64-byte signature; returns bool; rejects malformed inputs by length-check before NSec import. Cited line + signature exact ✓.

### §2.4 `PeerId.From(PrincipalId)` at PeerId.cs:13 — VERIFIED + A3 uses correct mapping

```csharp
// packages/federation-common/PeerId.cs
13:    public static PeerId From(PrincipalId principal) => new(principal.ToBase64Url());
```

Returns a `PeerId(string Value)` where `Value` is the **43-character base64url string** of the underlying 32-byte raw key. **This is the F1 finding from the A1 council review** — the original A1 prose mis-described `PeerId.From` as producing raw bytes. A1 was corrected to clarify the mapping; A3 explicitly mandates `PrincipalId.AsSpan()` (32 raw bytes), NOT `Encoding.UTF8.GetBytes(PeerId.Value)` (43 base64url-string bytes), as the HEARTBEAT signable input. Verified at §A3.6 audit table row 4: *"A3 §A3.4 Check 1 explicitly mandates `PrincipalId.AsSpan()` (raw 32B), NOT `Encoding.UTF8.GetBytes(PeerId.Value)` (43B base64url) — preserves A1 §A1.3 §A3 mandate"*. The vector V4–V6 `peer_id_raw_hex` field is the raw 32-byte hex string, not a base64url ASCII string. **A3 does NOT repeat A1's F1 defect.** ✓

### §2.5 `TenantId(string Value)` backing semantics — VERIFIED

```csharp
// packages/foundation/Assets/Common/TenantId.cs
8:  public readonly record struct TenantId(string Value)
```

Single field `Value` of type `string`. No UUID semantics. The A3 `confirm_transcript_input` and `hello_signable` and `heartbeat_signable` functions all use `tenant_value.encode("utf-8")` — UTF-8 byte representation of the .NET string. Cross-verified against §A1.3 §A2 ratified encoding (`tenantBytes = System.Text.Encoding.UTF8.GetBytes(TenantId.Value)`). ✓

(See M1 above for the orthogonal NFC-normalization gap, which is a property of `string Value` rather than of A3's vector authoring.)

### §2.6 Bonus citations verified

- **`Sunfish.Foundation.Channels.ChannelCapability`** — `packages/foundation-channels/ChannelCapability.cs` line 9: `[Flags] public enum ChannelCapability : byte { None=0, Text=1<<0, Audio=1<<1, Video=1<<2 }`. A3 vectors use the byte values 0x01/0x02/0x04/0x07 consistent with `Text|Audio|Video = 0x07`. ✓
- **`tools/icm/render-ledger.py` cohort precedent** — referenced for the byte-stable `--check` pattern. Cross-checked: `tools/icm/render-ledger.py --check` exits 0 against the post-A3 ledger state. Pattern faithfully replicated in `generate-channel-vectors.py` (read existing → diff → exit 0/1).

**Three-direction structural-citation summary:**
- **Positive existence (does cited symbol exist):** all 6 cited symbols verified at exact paths + line numbers ✓
- **Negative existence (no parallel-session pre-emption):** `gh pr list --search "ADR 0076"` shows PR #600 is the only open A3 PR; no parallel session ✓
- **Structural correctness (do byte layouts match cited spec):** all 3 byte-layout citations (HELLO signable, HEARTBEAT signable, CONFIRM transcript input) verified against §A1.3 + §A2.3 source-text ordering ✓

No 3-direction defect detected.

---

## §3 — Generator correctness (focus areas 11–12)

### §3.1 Determinism / no environmental leakage (focus area #11) — PASS

Read `tools/icm/generate-channel-vectors.py` (580 lines) end-to-end. Verified:

- **No timestamps:** the only timestamp in the output is the *fixed* `sample_timestamp_unix_ms = 1735689600000` (2025-01-01T00:00:00Z), not `time.time()` or any wall-clock read.
- **No file paths in output:** the output JSON contains the literal phrase strings (`"sunfish-channels-test-initiator-id-v1"` etc.) but not the absolute path of the script or the worktree. The `OUTPUT = ROOT / "tools" / "icm" / "channel-test-vectors.json"` path is used to *write* the file but is not embedded.
- **No environment variable reads:** no `os.environ`, no `os.getenv`, no `getpass.getuser()`, no `socket.gethostname()`. The script uses only `argparse` for `--check` flag.
- **Sort order is stable:** `json.dumps(vectors, indent=2, ensure_ascii=False)` with `sort_keys=False` (the default-true `sort_keys` is NOT set, but the dict is constructed in an explicit insertion order in `build_vectors()`). Python 3.7+ dicts preserve insertion order; the script's docstring header explicitly notes the deterministic-output guarantee. Two consecutive runs produce byte-identical output (verified above).
- **Hex normalization:** `bytes.hex()` produces lowercase hex by Python convention; the JSON file uses lowercase exclusively. The generator's `hex_(b)` helper at line 218 (`def hex_(b: bytes) -> str: return b.hex()`) is consistent.
- **`--check` mode actually re-runs generation and diffs:** verified at lines 562–571 — re-renders into `rendered`, reads `OUTPUT.read_text()` into `existing`, performs string equality comparison, exits 1 with a diagnostic message if they differ. No stale-hash trick. ✓

The `--check` semantics are exactly what cohort precedent (`render-ledger.py`) established. A `chore` commit that bumps `tools/icm/channel-test-vectors.json` by hand without re-running the generator would fail CI immediately if a CI gate runs `--check` (gate not yet wired but trivially addable).

### §3.2 Cross-impl reproducibility hint (focus area #12) — PASS

§A3.5 explicitly documents:
- **The canonical generator is Python `cryptography` package**, NOT the .NET reference impl, to avoid coupling the spec to NSec.Cryptography quirks (NSec wraps libsodium; PyCA wraps OpenSSL/libssl; both implement RFC 8032 / RFC 7748 / FIPS 180-4 deterministically; the spec is library-portable by construction).
- **Per-language fixture-loader pattern** (§A3.4 last paragraph): "Each language implements one loader (e.g. `EncryptionHandshakeConformanceTests` in xUnit for .NET; an equivalent in `XCTest` for Swift; in `kotest` for Kotlin) that reads the JSON, drives Checks 1–3, and reports per-vector pass/fail. The loader is implementation-private; the JSON is shared."
- **Seed phrases are ASCII strings** (`"sunfish-channels-test-initiator-id-v1"` etc.) so any implementer can independently SHA-256 them and verify the resolved seed/pubkey hex against `fixed_inputs`.

The discipline is sufficient to unblock a Swift / Kotlin / Rust impl. The §A3.4 conformance-protocol prose (Check 1 byte-assembly, Check 2 Ed25519 sig, Check 3 SHA-256 transcript) tells an implementer exactly how to validate their port.

**Cohort-precedent comparison:** The cohort's "interop-falsifiability" canon (RFC 8439 §2.1.1, RFC 7748 §6.1, RFC 5869 §A) all ship vectors as input/output hex with no source language privileged. A3 follows that pattern + adds the "phrase-derived seed" idiom for full source-reproducibility — slightly *stronger* than the IETF canon. PASS.

---

## §4 — Frontmatter / cohort idiom (focus areas 13–14)

### §4.1 `amendments:` list updated (focus area #13) — VERIFIED

ADR 0076 frontmatter at lines 30–62 has three amendment entries (A1 dated 2026-05-04 + A1 ratification dated 2026-05-05 + A2 dated 2026-05-05 + A3 dated 2026-05-05). The A3 entry (lines 55–62) summarizes:

```yaml
- date: 2026-05-05
  summary: >
    A3 conformance test vectors: ships 9 deterministic test vectors (HELLO ×3, HEARTBEAT ×3,
    CONFIRM-transcript-hash ×3) that fix the A1+A2 ratified canonical encoding to authoritative
    byte sequences. Closes A1 council finding F3 (interop-falsifiability gap). Vectors generated
    by tools/icm/generate-channel-vectors.py (re-runnable; byte-stable); committed JSON at
    tools/icm/channel-test-vectors.json. Per-implementation conformance protocol specified
    in §A3.5. Domain-separator (F2) remains deferred to a future amendment.
```

Shape matches A1 + A2 entries (date + summary; YAML block-scalar `>`; ASCII em-dash; ≤8 lines). Content accurately summarizes A3's deliverable + outstanding work. ✓

### §4.2 §A0 self-audit block (focus area #14) — VERIFIED

§A3.6 (`### A3.6 §A0 self-audit (per ADR 0069 D1 discipline)`) is present, lines 1199–1223. Includes:

- 8-row `Symbol/Path` table covering 6 existing + 2 new symbols
- §A0.1 negative-existence statement (no parallel-session A3 PR; no naming collision with PR #566's A2)
- §A0.2 false-positive statement (every cited Sunfish.* symbol opened on `origin/main` commit `7da6804` — verified)
- §A0.3 structural-correctness statement (uint32BE encoding + HEARTBEAT signable order + CONFIRM transcript order all cross-cited against §A1.3, §A2.3, §A2.5)

The block follows the cohort template (per `0028-A11-council-review`, `0046-A4-council-review`, etc.). All three §A0 sub-blocks are populated with non-trivial verification statements (not "n/a" placeholders). ✓

#### **M2 (Minor) — Pre-existing broken §A2.8 reference**

§A2.8 lines 989–990 cite: *"W#45 P4 council review: `icm/07_review/output/adr-audits/0076-a2-council-review-2026-05-05.md` (finding #8 origin)"*. This file **does not exist** on `origin/main` (verified by `ls icm/07_review/output/adr-audits/`). The W#45 P4 council that produced finding #8 was a **COB local-branch council** dispatched on `feat/w45-p4-blocks-crew-comms`, not a separate `icm/07_review/output/adr-audits/` artifact (analogous to the A1-era situation flagged in the A1 council's F9 finding).

This defect was introduced by **PR #566 (A2 amendment)**, not by A3. A3 inherits the broken citation as a passive reference but does not propagate it further. **This is not an A3-blocking finding** — A3 itself is correct on cited symbols. The fix (rewrite §A2.8 to point at the resolved beacon under `_archive/cob-question-2026-05-05T09-15Z-w45-p4-council-deferral-plan.md` or to flag explicitly that no separate council artifact exists) is appropriate for a follow-up `chore(adrs): 0076 §A2.8 reference correction` PR, or can be batched into a future amendment that touches the references section.

Recommendation: open a follow-up issue or roll the §A2.8 fix into the eventual F2-closing amendment. **Not a blocker for A3 merge.**

---

## §5 — Structural drift / unanticipated coupling (focus areas 15–16)

### §5.1 W#45 ledger row + per-workstream file edited (focus area #15) — VERIFIED

`git diff origin/main..HEAD -- icm/_state/active-workstreams.md icm/_state/workstreams/W45-*.md` shows two surgical edits:
- W#45 row's `reference_cell` extended from `(+ A1 PR #564 + A2 PR #566)` to `(+ A1 PR #564 + A2 PR #566 + A3 conformance vectors)`
- W#45 row's note appended with a 6-sentence A3 summary
- W#45 per-workstream file (`W45-crew-comms-*.md`) frontmatter `reference_cell` + body `## Notes` mirror the same edits

`python3 tools/icm/render-ledger.py --check` exits 0 — confirming `active-workstreams.md` is rendered consistent with the per-workstream file (per the cohort's W#42-era ledger render discipline). ✓

The "24-of-24 substrate amendments needing council fixes" cohort-tally line in W#45 is **not** updated — and that's correct, because it tracks **W#45 P-phase implementation council findings** (P2: 1 Critical + 4 Major; P4: 2 Critical + 8 Major), not amendment-level council findings on the ADR text. A3 is an *amendment* council, not a *P-phase* council. The 24-of-24 W#45 substrate count is independent of the global cohort 30-of-31 amendment count. ✓

### §5.2 Branch name vs content (focus area #16) — non-issue

Branch is `docs/adr-0076-a3-test-vectors` (per `gh pr view 600 --json headRefName` = `"docs/adr-0076-a3-test-vectors"` and `git status` shows local tracking branch matches). The parent prompt's claim that the branch was named `docs/adr-0076-a2-test-vectors` is stale — likely an artifact of the dispatcher's pre-renaming-rename context. Branch name aligns with content. ✓

(Cosmetic side note: even if a branch *were* mis-named, the cohort's commit/PR-title discipline only requires the *commit-prefix* + *PR-title-subject* match the conventional-commits enum + the actual amendment letter. PR #600's title is `docs(adrs): 0076 Amendment A3 — conformance test vectors (HELLO / HEARTBEAT / transcript-hash)` ✓; commit body is consistent with squash-merge. Branch-name strictness is not enforced by commitlint, by ADR 0069, or by any cohort precedent.)

---

## §6 — Verdict + cohort batting update + council recommendation

### §6.1 Verdict: **PASS**

A3 is mergeable as-authored. Two Minor findings (M1 NFC-gap, M2 broken §A2.8 reference) are deferrable; one Mechanical finding (M3 11,980-vs-12,001-byte off-by-21 in a single stdout line) is cosmetic. **No author-side amendment required for merge.** Author may optionally apply M3 in a `--amend`-equivalent fresh push (preferred over follow-up PR if the author has not yet merged); M1 + M2 should be a tracked follow-up.

### §6.2 Cohort batting update

**Global:** **30-of-31** (96.8%) substrate amendments needed council fixes → after A3 PASSes, **30-of-31** (96.8%); A3 is the **second** PASS-on-first-try.

The two PASS-on-first-try cases (ADR 0028 A11 + ADR 0076 A3) share three structural properties worth naming as a cohort observation:
1. **Zero new `Sunfish.*` types introduced.** Both amendments are pure specification artifacts (in A11's case, a feature-gate-off behavior amendment; in A3's case, a test-vector specification artifact).
2. **Author had a fully verifiable check available at authoring time.** A11 had analyzer-test parity; A3 had `--check` byte-stability + `python3 -c …` hand-recompute. The author *could and did* validate before pushing.
3. **Amendment text has a self-audit block (§A0) populated with non-trivial citations**, not boilerplate "n/a" placeholders. Both amendments verified each cited Sunfish.* symbol on `origin/main` at a specific commit.

Conjecture for cohort discipline going forward: "When all three of (zero-new-types, fully-verifiable-author-time-check, populated-§A0) hold, pre-merge council should expect ≤2 Minor + 0 Major findings. When any one is absent, expect ≥1 Major. Use this as a triage heuristic — substrate amendments that can satisfy all three should be authored toward the PASS-on-first-try profile."

This conjecture is testable with N=2 currently; will become statistically meaningful at N≥4. Worth tracking in `feedback_*.md` once a third PASS-on-first-try lands.

### §6.3 Council recommendation

**Author (XO) action:** **proceed to merge** PR #600. Optionally fix M3 (the script's `print(…)` byte/character distinction) as a 2-line `--amend`-equivalent fresh push (cohort discipline forbids `--amend` post-push; just push a tiny follow-up commit if you want it before merge). Track M1 + M2 as follow-up issues:
- M1 → file as Stage 02 architecture concern under W#45's open follow-ups; address in either F2-closing amendment or a standalone A4 "TenantId Unicode normalization" amendment.
- M2 → 5-minute editorial fix; can roll into the next ADR 0076 amendment or a `chore(adrs): 0076 §A2.8 reference correction` standalone PR.

**Sunfish-PM action:** A3 vectors are now the canonical reference for the W#45 P4.5 PR 1 (transcript-hash alignment). Consume them as xUnit known-answer fixtures via JSON loader at test-time; do NOT bake the hex strings inline into test code — the conformance-protocol §A3.4 mandates the JSON-as-shared-source-of-truth pattern. Ship the loader + 9 assertions in PR 1 as planned.

**XO author follow-ups:** queue but do not block on:
- A4 (or F2-folded amendment): TenantId Unicode normalization (NFC mandate at construction)
- §A2.8 reference correction (5-min editorial)
- F2 domain-separator amendment (next time a v2 protocol concern lands or W#23 iOS impl reaches handshake-byte-layout work)

**No re-council required.** A3 ships as-is.

---

## Appendix A — Hand-computed vector spot-check details

The full cleanroom Python session that hand-recomputed V1 + V5 + V7 (excerpted):

```python
import hashlib, struct
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.asymmetric.x25519 import X25519PrivateKey
from cryptography.hazmat.primitives import serialization

def seed(p): return hashlib.sha256(p.encode("utf-8")).digest()
def ed_pub(p):
    sk = Ed25519PrivateKey.from_private_bytes(seed(p))
    return sk, sk.public_key().public_bytes(serialization.Encoding.Raw, serialization.PublicFormat.Raw)
def xpub(p):
    sk = X25519PrivateKey.from_private_bytes(seed(p))
    return sk.public_key().public_bytes(serialization.Encoding.Raw, serialization.PublicFormat.Raw)

init_sk, init_pub = ed_pub("sunfish-channels-test-initiator-id-v1")
resp_sk, resp_pub = ed_pub("sunfish-channels-test-responder-id-v1")
init_eph_pub = xpub("sunfish-channels-test-initiator-ephem-v1")
resp_eph_pub = xpub("sunfish-channels-test-responder-ephem-v1")

# V1 HELLO
tb = "tenant-001-acme".encode("utf-8")
v1_signable = init_eph_pub + init_pub + struct.pack(">I", len(tb)) + tb + bytes([0x07])
assert v1_signable.hex() == ("005f111c8869fa005c1df5c8c775eb95a6a7dca4393e5df3ad152e017d78b23e"
                             "4dba7077e2cbb3f4b66e1fb8e07911c9110d918326e707f60b8494974e85db35"
                             "0000000f74656e616e742d3030312d61636d6507"), "V1 signable mismatch"
v1_sig = init_sk.sign(v1_signable)
assert v1_sig.hex() == ("b185c034c93a312a670fb46ae30b4818f5bde417fdb3e0695d6c6aa7cb94128b"
                        "51ff8bc03c75244ad4d28db3ee0828882c03eb6fec86246bbabfab55bd3b4101"), "V1 sig mismatch"

# V5 HEARTBEAT
tb5 = ("x" * 63).encode("utf-8")
v5_signable = (resp_pub + struct.pack(">I", 63) + tb5 + bytes([0x07])
               + struct.pack(">q", 1_735_689_600_000))
assert len(v5_signable) == 108, "V5 length mismatch"
# Spot-check the 0x3F length-prefix sentinel (LE-confused impl would write 0x3F000000)
assert v5_signable[32:36] == b'\x00\x00\x00\x3f', "V5 length-prefix endianness mismatch"

# V7 CONFIRM
tb7 = "tenant-001-acme".encode("utf-8")
v7_input = (init_eph_pub + init_pub + resp_eph_pub + resp_pub
            + struct.pack(">I", 15) + tb7
            + bytes([0x07, 0x01, 0x07, 0x03]))  # inviteCaps, negCap, presA, presB
v7_hash = hashlib.sha256(v7_input).digest()
assert v7_hash.hex() == "5c38292a921ea0bff9a3b20b49e255d8e8eb06579e1aaa44aa11ad539f03a8fb", "V7 hash mismatch"
```

All asserts pass. Vectors are byte-exact reproducible from the canonical phrase rule + RFC 8032 + RFC 7748 + FIPS 180-4 primitives.

## Appendix B — Files reviewed

- `docs/adrs/0076-crew-comms-foundation-channels.md` (full file, 1252 lines on branch; A3 appendix lines 997–1252)
- `tools/icm/generate-channel-vectors.py` (580 lines, full read)
- `tools/icm/channel-test-vectors.json` (189 lines / 12,001 bytes, full read)
- `icm/_state/workstreams/W45-crew-comms-real-time-peer-to-peer-crew-communication-for-anc.md` (4-line edit)
- `icm/_state/active-workstreams.md` (1-line edit on W#45 row)
- `packages/foundation/Crypto/PrincipalId.cs` (line 49 — `AsSpan()`)
- `packages/foundation/Crypto/KeyPair.cs` (lines 54, 67 — `Sign`, `VerifyRaw`)
- `packages/federation-common/PeerId.cs` (line 13 — `From(PrincipalId)`)
- `packages/foundation/Assets/Common/TenantId.cs` (line 8 — `record struct TenantId(string Value)`)
- `packages/foundation-channels/ChannelCapability.cs` (line 9 — `[Flags] enum : byte`)
- `git show origin/main:icm/07_review/output/adr-audits/0076-A1-council-review-2026-05-04.md` (F2/F3 origin context)
- `git show origin/main:docs/adrs/0076-crew-comms-foundation-channels.md` (A1+A2 as-merged drift check)

---

**Council file word count:** ~3,800 words.
**Reviewer credentials:** XO research session, Opus 4.7, `xhigh` effort, ADR 0069 D1/D2/D3 discipline.
**Co-Authored-By:** Claude Opus 4.7 <noreply@anthropic.com>
