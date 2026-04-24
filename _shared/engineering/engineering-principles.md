# Engineering Principles

**Status:** Accepted
**Last reviewed:** 2026-04-24
**Governs:** Every PR in kernel, foundation, federation, and accelerator packages that touches integration between components, cryptographic compositions, or serialization/wire formats. ADR authors comply with §3 (Open-format commitment) when introducing any new serialization format.
**Companion docs:** [`planning-framework.md`](planning-framework.md), [`code-review.md`](code-review.md), [`coding-standards.md`](coding-standards.md), [`supply-chain-security.md`](supply-chain-security.md), [`data-privacy.md`](data-privacy.md), [`testing-strategy.md`](testing-strategy.md), [`../../docs/adrs/`](../../docs/adrs/).
**Agent relevance:** Loaded by agents authoring code in `kernel-security`, `foundation-security`, CRDT integration, the sync-daemon wire protocol, or any PR introducing a new serialization format. Skip for pure UI/documentation changes.

Three operational disciplines that determine whether Sunfish implements all seven Kleppmann local-first properties, or joins the cohort of attempts that stall at 5 of 7.

These principles are not suggestions. They are the operational definition of what it takes for Sunfish to satisfy the seven properties end-to-end. PR reviewers enforce them. ADR authors comply with them. When in doubt, default to the more conservative reading.

---

## 1. Integration complexity, not component complexity

Every component Sunfish needs exists in open source today:

| Layer | Proven OSS components |
|---|---|
| CRDT merge | Yjs (via YDotNet), Automerge, Loro (when C# bindings mature) |
| Encryption at rest | SQLCipher, OS-native keystores (DPAPI, Keychain, libsecret) |
| Key derivation | Argon2id reference implementations |
| Peer discovery | mDNS libraries, WireGuard-mesh tools |
| Sync protocol | CBOR, established gossip anti-entropy patterns |
| Desktop shell | .NET MAUI Blazor Hybrid |

**The hard engineering problem is not building any component from scratch.** It is wiring them together with consistent invariants, especially:

- CRDT epoch transitions across a Flease-coordinated subset of records (the AP/CP boundary)
- Delta apply-back from sync daemon into `ICrdtDocument` without double-apply or lost operations
- Schema migration coordination across nodes running different versions
- Per-tenant isolation in Bridge without breaking the single-node abstraction

This is where projects die. It is engineering, not research. Write integration tests before refactoring component internals. Component purity without integration is a red flag.

**Enforcement:** Every new component added to a kernel package requires an integration test against an existing package. No exceptions.

---

## 2. Crypto discipline

Property 6 (security and privacy by default) is feasible only if Sunfish refuses to generate novel cryptography.

**Rule A — Audited libraries as opaque primitives.**

Use libsodium, age, the Argon2id reference implementation, and well-established SQLCipher. Do not substitute. Do not optimize. Do not re-derive. AI and developers alike treat these as black boxes with published guarantees.

**Rule B — Compositions require cryptographic review.**

The DEK/KEK hierarchy, the team attestation protocol, the role-key derivation chain, and the relay's zero-knowledge properties are compositions of primitives. AI can generate these correctly against a specification. But:

1. The specification must be written down (not implicit in code)
2. The specification must be read and approved by someone with cryptographic engineering credentials — not just a security-conscious developer
3. Any change to the specification follows the same review discipline, no matter how small

**The failure mode this prevents:** a project with good developers who "look up how Argon2id works" and implement a slightly-wrong variant ships a product with a quiet security bug that never surfaces until audit.

The solution is not to avoid cryptography. It is to avoid *inventing* cryptography.

**Enforcement:** Every PR that touches `foundation-security`, `kernel-security`, or the relay encryption path requires sign-off from a cryptographic engineering reviewer. This includes documentation changes that affect specification language. No exceptions.

---

## 3. Open-format commitment

Property 5 (the long now) has one product-level decision that can kill the architecture single-handedly:

> **Adopt Yjs's or Automerge's documented wire format. Do not invent a new one.**

Anytype satisfies five of the seven Kleppmann properties but fails Property 5 because its Any-Block export format is proprietary — full-fidelity export requires an Any-Block-aware consumer, and no competing app reads it natively. Sunfish must not repeat this mistake.

**Concrete rules:**

- Sunfish's CRDT wire format is Yjs (via YDotNet) or Automerge — chosen, not derived
- Export paths produce files readable by any Yjs/Automerge-aware application
- Custom extensions (if any) are documented publicly as open specifications before shipping
- "Sunfish format" is not a marketing term; it is a composition of existing open formats

**When the question arises** — "should we invent an optimized binary format for [X]?" — **the default answer is NO.** Extending existing formats is acceptable; inventing from scratch is not. If performance genuinely requires a novel format, that format ships with a public open specification, not after one.

**Enforcement:** Any PR introducing a new serialization format or modifying wire format requires an ADR documenting:

1. Why existing formats (Yjs, Automerge, CBOR standard, plain text) are insufficient
2. The new format's open specification
3. Where the specification is published for external review

No "internal format" without a public specification. The word "proprietary" does not appear in Sunfish's data format documentation.

---

## Why these three in particular

These principles were derived by applying the Universal Planning Framework anti-pattern scan to the Kleppmann seven properties and to Sunfish's current implementation state as of 2026-04. The scan identified three failure modes most likely to terminate a local-first project before demonstrating all seven:

- Treating integration as research (UPF anti-pattern #10, "first idea unchallenged")
- Unvalidated cryptographic assumptions (UPF anti-pattern #9, "skipping Stage 0")
- Closed data formats (UPF anti-pattern #21, "assumed facts without sources")

Anytype cleared 5.5 of 7 and stopped — on Property 5 (format) and Property 7 (license). These three principles are the discipline that separates 5.5 from 7.

---

## Architectural asymmetry — what Sunfish can do that Anytype cannot

Anytype is production-grade on operational axes Sunfish has not reached. But on two axes Sunfish is structurally ahead:

1. **License.** Sunfish core is MIT-licensed. Anytype's application layer is source-available (commercial use requires consent from the Any Association). Property 7 (ultimate ownership) has a binary answer here: Sunfish's license makes vendor-independence structural; Anytype's license leaves it contractual.

2. **Modularity.** Sunfish's framework-agnostic kernel and per-record CP/AP positioning is a more principled composition than Anytype's monolithic TypeScript application. This matters when the system needs to be adopted by enterprises with their own UI frameworks or governance models.

These advantages exist only if principles 1–3 above hold. Integration failure, crypto improvisation, or format proprietary-drift erase them instantly.

---

## References

- The Inverted Stack, Chapter 2 — Local-First: From Sync Toy to Serious Stack
- Kleppmann, Wiggins, van Hardenberg, McGranaghan (2019), "Local-first software: You own your data, in spite of the cloud"
- Kleppmann, "The past, present, and future of local-first," Local-First Conf 2024
- Universal Planning Framework rule (`../../.claude/rules/universal-planning.md`)
- Wallace, E. (2019), "How Figma's Multiplayer Technology Works" — the cautionary example for Rule 3
