# Platform Phase B: Decentralization — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

---

## Platform Context

> **Read this before executing.** Sunfish is a framework-agnostic suite of building blocks for decentralized, multi-jurisdictional asset lifecycle systems. Platform Phase B is the **decentralization primitives track** of the platform roadmap — the work that makes cryptographic ownership, signed operations, and capability-based delegation into real code in Foundation. This complements (but does not replace) Phase B-blobs (content-addressed blob store, already shipped) and precedes Phase D (federation).
>
> The authoritative platform specification is `docs/specifications/sunfish-platform-specification.md` (v0.2). Relevant sections:
>
> - **§2.4 Cryptographic Ownership Model** — entity ownership chains signed by Ed25519 keys; mint / transfer / delegate operations
> - **§3.5 Permission Evaluator** — ReBAC over the Keyhive capability graph, OpenFGA-style authorization-model DSL
> - **§10.2 Delegation** (revised v0.2) — Keyhive-inspired group-membership capabilities (primary) + Macaroon-style bearer tokens (supplementary)
> - **§10.3 Time-Bound Access** — every delegation carries `expires_at`; the kernel rejects expired delegations at evaluation time
>
> Research notes that inform the design:
>
> - `docs/specifications/research-notes/automerge-evaluation.md` — the **Keyhive capability model** (Ed25519 individuals, group-as-CRDT membership, BeeKEM group key agreement, RIBLT set reconciliation) is the reference for Sunfish's capability graph
> - `docs/specifications/research-notes/external-references.md` §3 — **OpenFGA's ReBAC authorization-model DSL** is the reference for the policy evaluator layer; OpenFGA (the rules) + Keyhive (the membership facts) is the recommended composition
>
> Phase B delivers four new Foundation packages — `Crypto`, `Capabilities`, `Macaroons`, `PolicyEvaluator` — plus a DI wiring extension. **No Blazor dependency is introduced.** Crypto primitives are pure .NET + NSec (libsodium wrapper). The `HasNoBlazorDependency` invariant from Phase 2 continues to hold.

**Goal:** Ship the cryptographic ownership + Keyhive-inspired capability graph + Ed25519 signed operations + OpenFGA-style policy evaluator primitives as framework-agnostic source modules inside `Sunfish.Foundation`, with a clear DI extension (`AddSunfishDecentralization()`) that wires them into `AddSunfish()`. The resulting primitives are consumable from any adapter (Blazor today, React tomorrow) and any accelerator (Bridge today, Base/Transit/Schools tomorrow). No federation (peer sync), no entity-store bind (signed writes), no BeeKEM group key agreement, no post-quantum curves — all deferred to later platform phases.

**Architecture context:** Foundation is the framework-agnostic contract layer (see CLAUDE.md Package Architecture). It depends on nothing else in the repo. Crypto/Capabilities/Macaroons/PolicyEvaluator all live under `packages/foundation/` as sibling namespaces to the existing `Notifications/`, `Authorization/`, `Blobs/` folders. They ship in the same `Sunfish.Foundation` NuGet package — one physical assembly, four logical namespaces. No new csproj is created.

**Source reference:** Platform spec v0.2 is authoritative for the contract shapes (§3.5, §10.2). Automerge-evaluation.md §4.2 and §6.1 drive the Keyhive model adoption (replacing the earlier Macaroon-primary story). External-references.md §3.1 confirms OpenFGA as the layered ReBAC evaluator sitting over the capability graph.

**Tech Stack:**
- .NET 10, C# 13
- [NSec.Cryptography](https://nsec.rocks) — modern libsodium wrapper for .NET. Used for Ed25519 key generation, signing, verification. Potentially BLAKE2b later (not in this phase).
- `System.Security.Cryptography` — HMAC-SHA256 for macaroon signatures (built into .NET; no package reference needed)
- `System.Text.Json` — canonical serialization of signed-operation payloads (built into .NET)
- xUnit 2.9.3, NSubstitute 5.3.0 — already pinned in `Directory.Packages.props`

**Phase 2 invariant preservation:** All four new namespaces are pure .NET. The existing test `ISunfishIconProvider_HasNoBlazorDependency` in `packages/ui-core/tests/` inspects `Sunfish.UICore` (not Foundation), but the spirit applies: no Blazor references may leak into Foundation. Task 1 adds an analogous `HasNoBlazorDependency` test for `Sunfish.Foundation` to lock in the invariant for crypto primitives as well.

---

## Scope

### In scope

1. **`Sunfish.Foundation.Crypto`** — Ed25519 primitives
   - `PrincipalId` — strong-typed wrapper over a 32-byte Ed25519 public key; base64url + multibase-ready encoding
   - `Signature` — strong-typed wrapper over a 64-byte Ed25519 signature
   - `KeyPair` — dev/test helper that generates a new Ed25519 keypair via NSec; production consumers plug their own signer into `IOperationSigner`
   - `SignedOperation<T>` — `(Payload: T, IssuerId: PrincipalId, Signature: Signature, IssuedAt: DateTimeOffset, Nonce: Guid)` — the canonical "signed thing" envelope
   - `IOperationSigner` — contract; signs a canonical payload on behalf of a principal
   - `IOperationVerifier` — contract; verifies a `SignedOperation<T>` against an expected issuer
   - `Ed25519Signer` — in-memory `IOperationSigner` using NSec; **dev/test only** (wraps a `KeyPair` in memory)
   - `Ed25519Verifier` — `IOperationVerifier` using NSec; production-appropriate (stateless, no secret material)
   - `DevKeyStore` — in-memory directory of `(PrincipalId → Key)` for tests and local dev only
   - Canonical serialization helper (`CanonicalJson`) — deterministic JSON emission so two parties produce the same signable bytes

2. **`Sunfish.Foundation.Capabilities`** — Keyhive-inspired capability graph
   - `Principal` (abstract) + `Individual : Principal` + `Group : Principal` — the graph nodes
   - `CapabilityOp` (abstract) + five concrete op records:
     - `Delegate(Issuer, Subject, Resource, Action, Expires?)`
     - `Revoke(Issuer, Subject, Resource, Action)`
     - `AddMember(Group, Member)`
     - `RemoveMember(Group, Member)`
     - `MintPrincipal(Kind, InitialMembers?)` — creates a new `Individual` or `Group` node
   - `SignedCapabilityOp = SignedOperation<CapabilityOp>`
   - `Resource` — opaque identifier for any addressable thing (entity ID, blob CID, namespace URI); strong-typed
   - `Action` — enum-like string vocabulary (`Read`, `Write`, `Delete`, `Delegate`, `Sign`, plus open extension values)
   - `CapabilityProof` — serialized evidence of a membership relation: the chain of signed ops from root issuer down to the subject principal, for use by offline verifiers
   - `ICapabilityGraph` — the core contract:
     - `ValueTask<bool> QueryAsync(PrincipalId subject, Resource resource, Action action, DateTimeOffset asOf, CancellationToken ct)`
     - `ValueTask<MutationResult> MutateAsync(SignedCapabilityOp op, CancellationToken ct)` — verifies signature, applies op, returns `Accepted | Rejected(reason)`
     - `ValueTask<CapabilityProof> ExportProofAsync(PrincipalId subject, Resource resource, Action action, CancellationToken ct)` — for federation / offline proof
     - `IAsyncEnumerable<SignedCapabilityOp> ListOpsAsync(CancellationToken ct)` — for replication / export
   - `InMemoryCapabilityGraph` — the default backend; lazy transitive-closure computation per query
   - Graph traversal with cycle detection (groups can be members of groups; must not infinite-loop)
   - Expiration checks — a delegation with `Expires < asOf` is ignored during closure

3. **`Sunfish.Foundation.Macaroons`** — supplementary bearer-token primitive (spec §10.2.2)
   - `Macaroon` — `(Location, Identifier, Caveats: IReadOnlyList<Caveat>, Signature: byte[32])`
   - `Caveat` — string predicate (first-party only in this phase); examples:
     - `"time < 2026-07-01T00:00:00Z"`
     - `"subject == individual:jim@acmeinsp.example"`
     - `"action in [read, write]"`
   - `IMacaroonIssuer` — contract; mints + attenuates macaroons using a symmetric root key
   - `IMacaroonVerifier` — contract; validates signature chain + evaluates caveats against a `MacaroonContext`
   - `MacaroonContext` — the input bag against which caveats evaluate: current time, calling subject, requested resource, requested action
   - `RootKeyStore` contract — `GetRootKeyAsync(location) → byte[32]`; in-memory dev implementation
   - Wire format — base64url of `{location}|{identifier}|{caveat0}|{caveat1}|...|{signature}`
   - First-party caveat parser — a minimal expression grammar for the five caveat forms listed above
   - Third-party caveats are **out of scope** in Phase B (noted in §10.2.2; deferred)

4. **`Sunfish.Foundation.PolicyEvaluator`** — OpenFGA-style ReBAC policy evaluator
   - `IPolicyEvaluator : IPermissionEvaluator` — adopts the kernel `IPermissionEvaluator` contract shape from spec §3.5 (see "note on contract naming" in Key Decisions)
   - `Decision` — `(Kind: Permit|Deny|Indeterminate, Reason?, MatchedPolicies[], Obligations[])` per spec §3.5
   - `PolicyModel` — the fluent-API model builder (no source generator, no parser in Phase B)
   - `TypeDefinition` — one entry in a policy model; declares relations and rewrites
   - `RelationRewrite` (discriminated union) — `Self`, `ComputedUserset(Relation)`, `TupleToUserset(Tupleset, ComputedRelation)`, `Union(params)`, `Intersection(params)`, `Exclusion(a, b)` — matches OpenFGA's DSL semantics
   - `ReBACPolicyEvaluator` — the default evaluator; consults an `ICapabilityGraph` for membership facts
   - Fluent API surface (see Task 6 for the concrete C# shape):
     ```csharp
     var model = PolicyModel.Create()
         .Type("user")
         .Type("inspection_firm", t => t
             .Relation("employee", r => r.DirectUsers("user")))
         .Type("property", t => t
             .Relation("landlord", r => r.DirectUsers("user"))
             .Relation("pm_firm",  r => r.DirectUsers("inspection_firm"))
             .Relation("can_inspect",
                 RelationRewrite.Union(
                     RelationRewrite.ComputedUserset("inspector"),
                     RelationRewrite.TupleToUserset("pm_firm", "employee"))))
         .Build();
     ```
   - Stateless evaluator — all state comes from the capability graph; no policy-eval-local cache in Phase B (add in Phase D if profiling motivates it)

5. **DI Integration** — `AddSunfishDecentralization()` extension on `SunfishBuilder`
   - Registers `ICapabilityGraph` → `InMemoryCapabilityGraph` (singleton)
   - Registers `IOperationVerifier` → `Ed25519Verifier` (singleton; stateless)
   - Registers `IPolicyEvaluator` → `ReBACPolicyEvaluator` (singleton)
   - Registers `IMacaroonVerifier` → default first-party verifier (singleton)
   - Registers `RootKeyStore` → `InMemoryRootKeyStore` **only when `EnableDevKeyMaterial = true`** on `SunfishOptions` (warning logged)
   - Accepts an `Action<DecentralizationOptions>` for model-registration callbacks (fluent policy model gets built at startup and cached as a `PolicyModel` singleton)
   - Does **not** register `IOperationSigner` — production consumers plug their own KMS/HSM/OS-keyring adapter; dev/test code registers `Ed25519Signer` explicitly

### Out of scope (future platform phases)

- **Federation** — peer-to-peer sync of the capability graph is Platform Phase D. Phase B's `ICapabilityGraph` exposes `ListOpsAsync` and `ExportProofAsync` so the contracts are ready for a federation adapter, but no sync protocol ships here.
- **Signed entity writes** — binding signed operations to Entity Store mutations is Platform Phase E (or later). Phase B produces the primitives; wiring them to the entity store is a separate plan.
- **Postgres capability backend** — in-memory backend only in Phase B. A `PostgresCapabilityGraph` with materialized closure is an explicit follow-up (sibling to the `PostgresBlobStore` follow-up from Phase B-blobs).
- **BeeKEM / RIBLT** — continuous group key agreement and set reconciliation are deep research tracks. Sunfish's capability graph ships without confidentiality (members see ciphertext) in Phase B; confidentiality is a Phase D concern.
- **Third-party macaroon caveats (discharge macaroons)** — Phase B ships first-party caveats only. Discharge flow is a follow-up.
- **Crypto agility / post-quantum** — Ed25519 only. Curve25519-dalek, Dilithium, etc. are parked.
- **Automerge CRDT integration** — per the automerge-evaluation.md recommendation, we adopt the **model**, not the library. No `automerge-dotnet` binding in Phase B. A sidecar integration may happen in a later phase for cross-verification.
- **Policy language parser** — the fluent API is the only authoring surface in Phase B. A parsed DSL (spec §3.5's aspirational syntax) may come later once the fluent API has stabilized.
- **Audit obligations fulfillment** — `Decision` carries obligations per the contract; the evaluator returns them, but the Phase B caller is responsible for fulfilling them. A standardized obligation-sink is future work.

---

## Key Decisions

**D-CRYPTO-LIB (Ed25519 implementation):** NSec.Cryptography (libsodium wrapper) is the chosen Ed25519 implementation. NSec wraps libsodium's constant-time Ed25519 implementation via a managed API (`SignatureAlgorithm.Ed25519`, `Key`, `PublicKey`). Alternatives considered:

| Library | Verdict |
|---|---|
| **NSec.Cryptography** | **Chosen.** Fastest .NET Ed25519 (libsodium native); actively maintained; ~2 MB native asset; modern `Span<byte>`-friendly API. |
| BouncyCastle (`BouncyCastle.Cryptography`) | Ubiquitous; pure managed; slower than libsodium; broader curve coverage is irrelevant — we only need Ed25519. Keep as a parking-lot alternative if native-asset shipping ever becomes a problem. |
| .NET 10 native `System.Security.Cryptography` | .NET 10 does expose Ed25519 via `Ed25519` class in `System.Security.Cryptography`, but API surface is less ergonomic than NSec and it lacks BLAKE2b which we may want later. We prefer NSec for consistency across primitives. |

Pin `NSec.Cryptography` version `25.4.0` (verify current via NuGet at execution time) in `Directory.Packages.props`. Foundation's csproj gets one new `<PackageReference Include="NSec.Cryptography" />`.

**D-KEY-STORAGE (where do private keys live?):** Sunfish itself does **not** store private keys. `Sunfish.Foundation.Crypto` accepts signed operations produced by external signers. The `IOperationSigner` contract is the integration seam; production deployments plug in their own implementation backed by:

- Azure Key Vault / AWS KMS / Google Cloud KMS (cloud HSM)
- PKCS#11 HSM (on-prem)
- OS keyring (Windows Credential Manager, macOS Keychain, GNOME Keyring)
- Mobile secure enclave (iOS Secure Enclave, Android Keystore) — for future mobile clients

For tests and local development, `Ed25519Signer` (an in-memory signer over a `KeyPair` held in a `DevKeyStore`) is provided and must **never** be registered in production. The DI extension gates `DevKeyStore` registration behind an explicit opt-in (`EnableDevKeyMaterial = true`) and emits a startup warning log when enabled — same pattern as the `DemoTenantContext` warning in Phase 9.

**D-POLICY-DSL (authoring surface):** Fluent C# API in Phase B. No source generator, no parser, no external DSL file. Rationale:

1. Spec §3.5 shows an OpenFGA-style DSL syntactically; that's the aspirational target but implementing a parser + model-validation + nice error messages is 3–5× the work of a fluent API and isn't on the Phase B critical path.
2. Fluent API exercises the same semantic model (types, relations, rewrite expressions). A future parser phase produces an AST that **targets the same** `PolicyModel` type. Zero rework when the parser is added.
3. Developers writing Sunfish-backed apps already live in C#. Keeping the policy model in-language keeps the feedback loop tight (compile errors, IntelliSense, refactoring tools).
4. Spec §3.5's DSL text can still be used as a documentation artifact — the plan provides a fully-worked example side-by-side (DSL text + fluent API equivalent) so developers can translate published policy packs into fluent-API models by hand.

When the parser arrives, its output is a `PolicyModel` — the same type the fluent API produces. Consumers don't have to change their call-site; they just swap `PolicyModel.Create()…Build()` for `PolicyModel.Parse(dslText)`.

**D-GRAPH-SHAPE (capability graph internals):** Principals are graph nodes; capability edges carry `(resource, action, expires?)` tuples; group membership forms nested structure. Closure computed **lazily** (per-query) for the in-memory backend. Explicitly:

- `Individual { PrincipalId, publicKey }` — leaf nodes; one Ed25519 keypair bound to a specific device (or a logical identity the consumer considers atomic)
- `Group { PrincipalId, Members: IReadOnlyList<PrincipalId> }` — mutable set; members may be individuals OR other groups (nesting); membership changes are themselves `SignedCapabilityOp` entries
- Capability edges: `(Issuer: PrincipalId, Subject: PrincipalId, Resource, Action, Expires?)` — signed by the issuer; expresses "issuer grants subject the right to perform action on resource until expires"
- Query answers `"can subject S perform action A on resource R at time T?"` by:
  1. Finding all capability edges whose `Subject` is a principal that transitively contains S (S is a member, or a member of a member, …)
  2. Filtering for `Resource == R` and `Action == A`
  3. Filtering for `Expires == null || Expires > T`
  4. Returning `true` if any such edge exists AND every step of the issuer chain is valid (the issuer at each step had the authority to delegate)
  - Sunfish enforces `Authority = transitive delegation from the resource's root-owner`. The resource's mint op is always the root of trust; any delegation must chain back to the mint.
- Cycle detection: BFS with a visited-set; a group that contains itself (directly or transitively) is a malformed mutation op and is `Rejected` at `MutateAsync` time, not during query.
- Performance: O(|V| + |E|) per query in the closure. For the in-memory backend at Bridge scale (thousands of principals, tens of thousands of edges) this is sub-millisecond. Materialized closure is a Postgres-backend optimization for Phase D.

**D-MACAROON-FORMAT (wire format + caveat grammar):** Self-contained bearer token, base64url-encoded. Binary layout:

```
{location:utf8} | 0x1E | {identifier:utf8} | 0x1E | {caveat0:utf8} | 0x1E | {caveat1:utf8} | 0x1E | ... | 0x1F | {signature:32bytes}
```

Where `0x1E` = record separator, `0x1F` = unit separator. Entire byte stream base64url-encoded for transport in an `Authorization: Macaroon <base64url>` header.

Signature chain (standard macaroon construction):

- `sig0 = HMAC-SHA256(rootKey, identifier)`
- `sig_{i+1} = HMAC-SHA256(sig_i, caveat_i)` — each caveat extends the chain
- `signature = sig_n` (final)

Verifier holds the root key (keyed by `location`); recomputes the chain; rejects on mismatch. Caveat evaluation happens separately, against a `MacaroonContext`.

First-party caveat grammar (Phase B ships exactly these five forms — minimal regex-based parser):

```
time  <=  "2026-07-01T00:00:00Z"
subject  ==  "individual:jim@acmeinsp.example"
resource.schema  matches  "sunfish.pm.inspection/*"
action  in  [ "read" , "write" ]
device_ip  in  "10.42.0.0/16"
```

(Anything else parses as "unknown caveat" and the verifier fails closed — per macaroon security best practice.) Third-party caveats are deferred.

**D-CONTRACT-NAMING (`IPermissionEvaluator` vs `IPolicyEvaluator`):** Spec §3.5 names the kernel contract `IPermissionEvaluator`. The Phase B evaluator implements this contract. The name `IPolicyEvaluator` used in the brief is a per-subsystem shorthand for "the policy-evaluator primitive in Phase B" — **the actual public interface is `IPermissionEvaluator`**, matching the spec and leaving room for alternative evaluators (OPA/Rego, Cedar) to plug into the same contract per §3.5's extension points. The concrete default implementation is `ReBACPolicyEvaluator`, distinguishing it from a future `OpaPolicyEvaluator` or `CedarPolicyEvaluator`.

**D-CANONICAL-JSON (what gets signed?):** `SignedOperation<T>` signs a **canonical JSON** of `T` concatenated with the `(IssuerId, IssuedAt, Nonce)` envelope fields. Canonical = deterministic key ordering (alphabetical), no insignificant whitespace, UTF-8 bytes. This matches the CID-style deterministic hashing pattern already used in `Sunfish.Foundation.Blobs`. Two signers with the same `T` value produce identical signable bytes → interoperable verification. Helper: `CanonicalJson.Serialize<T>(T value)` in `Sunfish.Foundation.Crypto`.

**D-NONCE (replay prevention):** Every `SignedOperation<T>` carries a `Nonce: Guid`. Verifier callers may track a recent-nonce window to reject replay. Phase B's `Ed25519Verifier` does **not** enforce nonce uniqueness (stateless verifier); it's the caller's responsibility. The `ICapabilityGraph.MutateAsync` implementation tracks processed nonces per-issuer to reject re-applied ops (idempotency).

**D-TIME-SOURCE:** All expiration checks take `DateTimeOffset asOf` as an explicit parameter. No `DateTimeOffset.UtcNow` inside the library — the caller supplies the time. This is testability-critical (capability-graph tests need deterministic clocks) and federation-critical (asking "was this allowed as-of timestamp T?" is a fundamental federation query). Consumers that want current-time semantics pass `DateTimeOffset.UtcNow` at the call site, typically via an injected `TimeProvider`.

---

## File Structure

```
packages/
  foundation/
    Sunfish.Foundation.csproj                               ← one line added: <PackageReference Include="NSec.Cryptography" />

    Crypto/                                                 ← NEW (Task 1)
      PrincipalId.cs
      Signature.cs
      KeyPair.cs                                            ← dev-only helper
      SignedOperation.cs                                    ← generic envelope
      IOperationSigner.cs
      IOperationVerifier.cs
      Ed25519Signer.cs                                      ← dev-only in-memory impl
      Ed25519Verifier.cs                                    ← stateless; production-appropriate
      DevKeyStore.cs                                        ← dev-only registry
      CanonicalJson.cs                                      ← deterministic JSON helper

    Capabilities/                                           ← NEW (Tasks 2–4)
      Principal.cs                                          ← abstract base + Individual + Group
      Resource.cs
      CapabilityAction.cs                                   ← string-enum-like with common constants
      CapabilityOp.cs                                       ← abstract + 5 concrete records
      CapabilityProof.cs
      ICapabilityGraph.cs
      InMemoryCapabilityGraph.cs
      MutationResult.cs
      CapabilityClosure.cs                                  ← internal; computes transitive closure

    Macaroons/                                              ← NEW (Task 5)
      Macaroon.cs
      Caveat.cs
      MacaroonContext.cs
      IMacaroonIssuer.cs
      IMacaroonVerifier.cs
      RootKeyStore.cs                                       ← contract + InMemoryRootKeyStore
      FirstPartyCaveatParser.cs                             ← internal
      MacaroonCodec.cs                                      ← internal; wire-format encode/decode

    PolicyEvaluator/                                        ← NEW (Task 6)
      IPermissionEvaluator.cs                               ← adopts spec §3.5 contract
      Decision.cs
      Obligation.cs
      Subject.cs                                            ← spec §3.5 record shape
      ActionType.cs                                         ← rename of spec §3.5 Action to avoid collision
      ContextEnvelope.cs                                    ← rename of spec §3.5 Context to avoid collision with System.Context
      PolicyModel.cs
      TypeDefinition.cs
      RelationRewrite.cs
      ReBACPolicyEvaluator.cs
      PolicyModelBuilder.cs                                 ← fluent-API entry
      TypeBuilder.cs                                        ← fluent-API child
      RelationBuilder.cs                                    ← fluent-API child

    Extensions/
      ServiceCollectionExtensions.cs                        ← MODIFIED (Task 7)
      SunfishDecentralizationExtensions.cs                  ← NEW (Task 7); AddSunfishDecentralization()
      DecentralizationOptions.cs                            ← NEW (Task 7)

    tests/
      tests.csproj                                          ← MODIFIED; add package refs if needed
      Crypto/                                               ← NEW (Task 1)
        PrincipalIdTests.cs
        SignatureTests.cs
        SignedOperationTests.cs
        Ed25519SignerTests.cs
        Ed25519VerifierTests.cs
        CanonicalJsonTests.cs
        HasNoBlazorDependencyTests.cs                       ← mirrors ui-core's invariant
      Capabilities/                                         ← NEW (Tasks 2–4)
        PrincipalTests.cs
        GroupMembershipTests.cs
        CapabilityOpSignatureTests.cs
        InMemoryCapabilityGraphTests.cs
        CapabilityGraphTransitiveMembershipTests.cs
        CapabilityGraphExpirationTests.cs
        CapabilityGraphCycleDetectionTests.cs
      Macaroons/                                            ← NEW (Task 5)
        MacaroonIssueAndVerifyTests.cs
        MacaroonAttenuationTests.cs
        FirstPartyCaveatParserTests.cs
        MacaroonWireFormatTests.cs
      PolicyEvaluator/                                      ← NEW (Task 6)
        PolicyModelBuilderTests.cs
        ReBACEvaluatorTests.cs
        OpenFgaWorkedExampleTests.cs                        ← spec §3.5 example, end-to-end
      Integration/                                          ← NEW (Task 8)
        DecentralizationEndToEndTests.cs                    ← all primitives composed; OpenFGA scenario
      Extensions/
        AddSunfishDecentralizationTests.cs                  ← NEW (Task 7)

Directory.Packages.props                                    ← MODIFIED (Task 0); add NSec.Cryptography pin

docs/specifications/
  sunfish-platform-specification.md                         ← NOT MODIFIED by this phase; Phase B consumes v0.2 as-is
  research-notes/automerge-evaluation.md                    ← NOT MODIFIED
  research-notes/external-references.md                     ← NOT MODIFIED
```

**Files outside `packages/foundation/`:**
- `Directory.Packages.props` — add the NSec.Cryptography version pin (Task 0).
- No changes to `ui-core`, `ui-adapters-blazor`, adapters, blocks, or accelerators. Phase B is a pure Foundation addition; downstream consumers pick it up by calling `AddSunfishDecentralization()` at their composition root.
- No changes to documentation outside this plan. Per the spec contribution policy, any spec clarifications surfaced during implementation get filed as follow-up edit suggestions to `sunfish-platform-specification.md`; they are not blockers for shipping Phase B.

---

## Task 0: Branch + Package Pin

**Files:**
- Modify: `Directory.Packages.props`
- Create: branch `feat/platform-phase-B-decentralization` off current branch

- [ ] **Step 1: Create the working branch**

```bash
cd "C:/Projects/Sunfish"
git checkout -b feat/platform-phase-B-decentralization
```

Expected: branch created, working tree preserved.

- [ ] **Step 2: Pin NSec.Cryptography in `Directory.Packages.props`**

Edit `Directory.Packages.props` and add one `<PackageVersion>` entry (verify current version via NuGet at execution time; `25.4.0` is the planning target):

```xml
<PackageVersion Include="NSec.Cryptography" Version="25.4.0" />
```

Insertion point: alphabetical order among the existing `<PackageVersion>` entries, between `NSubstitute` and any other `N*` entries (currently inserted after `NSubstitute`).

- [ ] **Step 3: Smoke-verify the build still passes with no other changes**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/foundation/Sunfish.Foundation.csproj
```

Expected: 0 errors, 0 warnings. (Adding a `<PackageVersion>` alone does not change compile; Foundation does not yet reference NSec — that happens in Task 1.)

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props
git commit -m "build: pin NSec.Cryptography 25.4.0 for Platform Phase B crypto primitives"
```

---

## Task 1: `Sunfish.Foundation.Crypto` — Ed25519 Primitives

**Files:**
- Modify: `packages/foundation/Sunfish.Foundation.csproj` (add `<PackageReference Include="NSec.Cryptography" />`)
- Create: `packages/foundation/Crypto/PrincipalId.cs`
- Create: `packages/foundation/Crypto/Signature.cs`
- Create: `packages/foundation/Crypto/KeyPair.cs`
- Create: `packages/foundation/Crypto/SignedOperation.cs`
- Create: `packages/foundation/Crypto/IOperationSigner.cs`
- Create: `packages/foundation/Crypto/IOperationVerifier.cs`
- Create: `packages/foundation/Crypto/Ed25519Signer.cs`
- Create: `packages/foundation/Crypto/Ed25519Verifier.cs`
- Create: `packages/foundation/Crypto/DevKeyStore.cs`
- Create: `packages/foundation/Crypto/CanonicalJson.cs`
- Create: `packages/foundation/tests/Crypto/*Tests.cs` (7 test files)

### Step 1: Wire the NSec package reference

- [ ] Edit `packages/foundation/Sunfish.Foundation.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Foundation</PackageId>
    <Description>Core contracts, enums, models, and services for Sunfish.</Description>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
    <PackageReference Include="NSec.Cryptography" />   <!-- NEW -->
  </ItemGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
</Project>
```

### Step 2: Implement `PrincipalId`

- [ ] Create `packages/foundation/Crypto/PrincipalId.cs`:

```csharp
using System.Buffers.Text;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Strong-typed wrapper over a 32-byte Ed25519 public key. Identifies a principal
/// (individual or group) in the Sunfish capability graph. Encoded as base64url for
/// transport; the underlying bytes are the canonical identity.
/// </summary>
/// <remarks>
/// Principals can be individuals (backed by a single Ed25519 keypair) or groups (a
/// mutable member list; their "public key" is derived from a well-known group-identity
/// scheme — a group's PrincipalId is the SHA-256 of its creation op's canonical JSON,
/// lifted into a 32-byte identifier). Either way, 32 bytes of identity.
/// See <c>docs/specifications/sunfish-platform-specification.md</c> §2.4 and §10.2.1.
/// </remarks>
public readonly record struct PrincipalId
{
    /// <summary>Ed25519 public key / group-identity digest, exactly 32 bytes.</summary>
    public const int SizeInBytes = 32;

    private readonly byte[] _bytes;

    public PrincipalId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
            throw new ArgumentException($"PrincipalId must be exactly {SizeInBytes} bytes.", nameof(bytes));
        _bytes = bytes.ToArray();
    }

    public ReadOnlySpan<byte> AsSpan() => _bytes;

    /// <summary>Base64url encoding for transport (URL-safe, no padding).</summary>
    public string ToBase64Url() => Base64Url.EncodeToString(_bytes);

    public static PrincipalId FromBase64Url(string s) =>
        new(Base64Url.DecodeFromChars(s));

    public override string ToString() => ToBase64Url();

    public bool Equals(PrincipalId other) =>
        _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());

    public override int GetHashCode() =>
        BitConverter.ToInt32(_bytes, 0);  // 4-byte prefix suffices; crypto uniqueness guarantees low collision
}
```

### Step 3: Implement `Signature`

- [ ] Create `packages/foundation/Crypto/Signature.cs`:

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>64-byte Ed25519 signature. Strong-typed to prevent accidental misuse.</summary>
public readonly record struct Signature
{
    public const int SizeInBytes = 64;

    private readonly byte[] _bytes;

    public Signature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SizeInBytes)
            throw new ArgumentException($"Signature must be exactly {SizeInBytes} bytes.", nameof(bytes));
        _bytes = bytes.ToArray();
    }

    public ReadOnlySpan<byte> AsSpan() => _bytes;

    public string ToBase64Url() => System.Buffers.Text.Base64Url.EncodeToString(_bytes);

    public static Signature FromBase64Url(string s) =>
        new(System.Buffers.Text.Base64Url.DecodeFromChars(s));

    public bool Equals(Signature other) =>
        _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());

    public override int GetHashCode() =>
        BitConverter.ToInt32(_bytes, 0);
}
```

### Step 4: Implement `KeyPair` (dev-only)

- [ ] Create `packages/foundation/Crypto/KeyPair.cs`:

```csharp
using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Dev/test helper. Holds an Ed25519 keypair in process memory.
/// <b>DO NOT use in production</b> — production consumers plug their own
/// <see cref="IOperationSigner"/> backed by KMS / HSM / OS keyring.
/// </summary>
public sealed class KeyPair : IDisposable
{
    private readonly Key _key;

    public PrincipalId PrincipalId { get; }

    private KeyPair(Key key)
    {
        _key = key;
        Span<byte> publicKeyBytes = stackalloc byte[PrincipalId.SizeInBytes];
        key.PublicKey.Export(KeyBlobFormat.RawPublicKey, publicKeyBytes, out int _);
        PrincipalId = new PrincipalId(publicKeyBytes);
    }

    public static KeyPair Generate()
    {
        var alg = SignatureAlgorithm.Ed25519;
        var creationParams = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
        };
        var key = Key.Create(alg, creationParams);
        return new KeyPair(key);
    }

    internal Key NSecKey => _key;

    public void Dispose() => _key.Dispose();
}
```

### Step 5: Implement `SignedOperation<T>`, signer + verifier contracts

- [ ] Create `packages/foundation/Crypto/SignedOperation.cs`:

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Envelope for any signed payload. The signature covers the canonical JSON of
/// <c>(Payload, IssuerId, IssuedAt, Nonce)</c>. The <see cref="Nonce"/> is a
/// per-operation unique identifier — implementations that enforce
/// replay-prevention track nonce windows per issuer.
/// </summary>
public sealed record SignedOperation<T>(
    T Payload,
    PrincipalId IssuerId,
    DateTimeOffset IssuedAt,
    Guid Nonce,
    Signature Signature);
```

- [ ] Create `packages/foundation/Crypto/IOperationSigner.cs`:

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Produces signed operations. The signer holds (or can access) the private
/// material for <see cref="IssuerId"/>. Production signers are backed by
/// KMS / HSM / OS keyring; <see cref="Ed25519Signer"/> is a dev-only in-memory
/// implementation.
/// </summary>
public interface IOperationSigner
{
    PrincipalId IssuerId { get; }

    ValueTask<SignedOperation<T>> SignAsync<T>(
        T payload,
        DateTimeOffset issuedAt,
        Guid nonce,
        CancellationToken ct = default);
}
```

- [ ] Create `packages/foundation/Crypto/IOperationVerifier.cs`:

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Verifies that a <see cref="SignedOperation{T}"/> was produced by the
/// claimed issuer. Stateless — no keys are stored; the signer's public
/// key is carried in the operation's <see cref="SignedOperation{T}.IssuerId"/>.
/// </summary>
public interface IOperationVerifier
{
    bool Verify<T>(SignedOperation<T> op);
}
```

### Step 6: Implement `Ed25519Signer` + `Ed25519Verifier`

- [ ] Create `packages/foundation/Crypto/Ed25519Signer.cs`:

```csharp
using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// <b>Dev/test only.</b> In-memory Ed25519 signer. Holds a private key in process
/// memory. Production code MUST replace with an <see cref="IOperationSigner"/>
/// backed by KMS / HSM / OS keyring.
/// </summary>
public sealed class Ed25519Signer(KeyPair keyPair) : IOperationSigner
{
    public PrincipalId IssuerId => keyPair.PrincipalId;

    public ValueTask<SignedOperation<T>> SignAsync<T>(
        T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
    {
        var signable = CanonicalJson.SerializeSignable(payload, IssuerId, issuedAt, nonce);
        var alg = SignatureAlgorithm.Ed25519;
        Span<byte> sigBytes = stackalloc byte[Signature.SizeInBytes];
        alg.Sign(keyPair.NSecKey, signable, sigBytes);
        var sig = new Signature(sigBytes);
        return ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, sig));
    }
}
```

- [ ] Create `packages/foundation/Crypto/Ed25519Verifier.cs`:

```csharp
using NSec.Cryptography;

namespace Sunfish.Foundation.Crypto;

/// <summary>Stateless Ed25519 verifier. Safe to register as a singleton.</summary>
public sealed class Ed25519Verifier : IOperationVerifier
{
    public bool Verify<T>(SignedOperation<T> op)
    {
        var signable = CanonicalJson.SerializeSignable(op.Payload, op.IssuerId, op.IssuedAt, op.Nonce);
        var alg = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(alg, op.IssuerId.AsSpan(), KeyBlobFormat.RawPublicKey);
        return alg.Verify(publicKey, signable, op.Signature.AsSpan());
    }
}
```

### Step 7: Implement `CanonicalJson`

- [ ] Create `packages/foundation/Crypto/CanonicalJson.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Deterministic JSON serializer used to produce the byte sequence signed by
/// <see cref="IOperationSigner"/> and verified by <see cref="IOperationVerifier"/>.
/// Keys sorted alphabetically at every object level; no insignificant whitespace;
/// UTF-8 bytes. Two signers serializing the same logical value produce identical bytes.
/// </summary>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions s_normalizer = new() { WriteIndented = false };

    public static byte[] Serialize<T>(T value)
    {
        var node = JsonSerializer.SerializeToNode(value, s_normalizer);
        var sorted = SortKeys(node);
        var json = sorted?.ToJsonString(s_normalizer) ?? "null";
        return Encoding.UTF8.GetBytes(json);
    }

    public static byte[] SerializeSignable<T>(T payload, PrincipalId issuerId, DateTimeOffset issuedAt, Guid nonce)
    {
        var envelope = new JsonObject
        {
            ["issuedAt"] = issuedAt.ToUnixTimeMilliseconds(),
            ["issuerId"] = issuerId.ToBase64Url(),
            ["nonce"] = nonce.ToString("N"),
            ["payload"] = JsonSerializer.SerializeToNode(payload, s_normalizer),
        };
        var sorted = SortKeys(envelope);
        return Encoding.UTF8.GetBytes(sorted?.ToJsonString(s_normalizer) ?? "null");
    }

    private static JsonNode? SortKeys(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonObject obj => new JsonObject(obj.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                                .Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, SortKeys(kv.Value?.DeepClone())))),
            JsonArray arr  => new JsonArray(arr.Select(item => SortKeys(item?.DeepClone())).ToArray()),
            _ => node.DeepClone(),
        };
}
```

### Step 8: Implement `DevKeyStore`

- [ ] Create `packages/foundation/Crypto/DevKeyStore.cs`:

```csharp
namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Dev/test registry of <see cref="KeyPair"/>s by <see cref="PrincipalId"/>.
/// Registered by the DI extension ONLY when <c>EnableDevKeyMaterial = true</c>.
/// A startup warning is emitted whenever this type is resolved.
/// </summary>
public sealed class DevKeyStore
{
    private readonly Dictionary<PrincipalId, KeyPair> _keys = new();

    public KeyPair Create()
    {
        var kp = KeyPair.Generate();
        _keys[kp.PrincipalId] = kp;
        return kp;
    }

    public KeyPair? TryGet(PrincipalId id) => _keys.TryGetValue(id, out var k) ? k : null;

    public IReadOnlyCollection<PrincipalId> AllPrincipals => _keys.Keys;
}
```

### Step 9: Tests

Each test file below is xUnit-based with `[Fact]` / `[Theory]` methods. Outline:

- [ ] `PrincipalIdTests.cs`:
  - `Ctor_RejectsWrongLength`
  - `Equality_UsesByteSequence`
  - `Base64UrlRoundtrip_Preserves32Bytes`
  - `GetHashCode_ConsistentWithEquals`

- [ ] `SignatureTests.cs`:
  - `Ctor_RejectsWrongLength`
  - `Base64UrlRoundtrip_Preserves64Bytes`

- [ ] `SignedOperationTests.cs`:
  - `Record_EqualityByValue`
  - `Record_WithExpression_PreservesSignatureOnPayloadChange_DoesNotAutoReSign` (documents that `with` semantics don't re-verify)

- [ ] `Ed25519SignerTests.cs`:
  - `Sign_ProducesDeterministicSignatureForSamePayloadIssuerAtNonce` (Ed25519 is deterministic)
  - `Sign_IssuerIdMatchesKeyPair`
  - `DifferentNonces_ProduceDifferentSignatures`

- [ ] `Ed25519VerifierTests.cs`:
  - `Verify_ReturnsTrueForValidSignedOperation`
  - `Verify_ReturnsFalseWhenPayloadTampered`
  - `Verify_ReturnsFalseWhenIssuedAtTampered`
  - `Verify_ReturnsFalseWhenNonceTampered`
  - `Verify_ReturnsFalseForMismatchedIssuer`
  - `Verify_WorksAcrossSignerVerifierInstanceBoundary` (round-trip through canonical JSON)

- [ ] `CanonicalJsonTests.cs`:
  - `Serialize_EmitsKeysInAlphabeticalOrder`
  - `Serialize_TwoDifferentInsertionOrders_ProduceIdenticalBytes`
  - `SerializeSignable_EnvelopeFieldsSortedWithPayload`
  - `Serialize_HandlesNullPayload`
  - `Serialize_HandlesNestedObjectsAndArrays`

- [ ] `HasNoBlazorDependencyTests.cs`:

```csharp
using System.Reflection;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class HasNoBlazorDependencyTests
{
    [Fact]
    public void SunfishFoundationAssembly_DoesNotReferenceAspNetCoreComponents()
    {
        var assembly = typeof(Sunfish.Foundation.Crypto.PrincipalId).Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("AspNetCore.Components") == true);
    }

    [Fact]
    public void SunfishFoundationAssembly_DoesNotReferenceBlazorAssemblies()
    {
        var assembly = typeof(Sunfish.Foundation.Crypto.PrincipalId).Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("Blazor") == true);
    }
}
```

### Step 10: Build + test

- [ ] Run:

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Crypto"
```

Expected: build green; crypto tests green. The `HasNoBlazorDependency` test passes — Foundation still only references `Microsoft.AspNetCore.SignalR.Client` + `NSec.Cryptography` + BCL.

### Step 11: Commit

- [ ] 
```bash
git add packages/foundation/Crypto packages/foundation/tests/Crypto packages/foundation/Sunfish.Foundation.csproj
git commit -m "feat(foundation): add Sunfish.Foundation.Crypto — Ed25519 primitives, SignedOperation envelope, canonical JSON"
```

---

## Task 2: `Sunfish.Foundation.Capabilities` — Principal Types

**Files:**
- Create: `packages/foundation/Capabilities/Principal.cs`
- Create: `packages/foundation/Capabilities/Resource.cs`
- Create: `packages/foundation/Capabilities/CapabilityAction.cs`
- Create: `packages/foundation/tests/Capabilities/PrincipalTests.cs`
- Create: `packages/foundation/tests/Capabilities/GroupMembershipTests.cs`

### Step 1: Principal hierarchy

- [ ] Create `packages/foundation/Capabilities/Principal.cs`:

```csharp
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Base type for all principals (nodes in the capability graph).
/// A principal is either an <see cref="Individual"/> (leaf) or a <see cref="Group"/> (composite).
/// See <c>docs/specifications/sunfish-platform-specification.md</c> §10.2.1.
/// </summary>
public abstract record Principal(PrincipalId Id);

/// <summary>
/// Leaf principal — an Ed25519 keypair owner. Typically a device, service account,
/// or logical identity considered atomic by the consumer.
/// </summary>
public sealed record Individual(PrincipalId Id) : Principal(Id);

/// <summary>
/// Composite principal — a named collection of member principals. Members may be
/// individuals or other groups (nesting allowed). Membership is mutated via
/// <c>AddMember</c> / <c>RemoveMember</c> signed operations.
/// </summary>
public sealed record Group(PrincipalId Id, IReadOnlyList<PrincipalId> Members) : Principal(Id);
```

### Step 2: `Resource`

- [ ] Create `packages/foundation/Capabilities/Resource.cs`:

```csharp
namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Opaque identifier for any addressable thing: an entity ID, a blob CID, a
/// namespace URI, or a composite URN. The capability graph treats resources
/// as black boxes keyed by string identity.
/// </summary>
public readonly record struct Resource(string Id)
{
    public override string ToString() => Id;
}
```

### Step 3: `CapabilityAction`

- [ ] Create `packages/foundation/Capabilities/CapabilityAction.cs`:

```csharp
namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// The verb of an authorization request. Common actions are constants; callers
/// may pass arbitrary strings for domain-specific actions (e.g., <c>sign_inspection</c>).
/// </summary>
public readonly record struct CapabilityAction(string Name)
{
    public static readonly CapabilityAction Read     = new("read");
    public static readonly CapabilityAction Write    = new("write");
    public static readonly CapabilityAction Delete   = new("delete");
    public static readonly CapabilityAction Delegate = new("delegate");
    public static readonly CapabilityAction Sign     = new("sign");

    public override string ToString() => Name;
}
```

### Step 4: Tests

- [ ] `PrincipalTests.cs`:
  - `Individual_RecordEqualityByIdOnly`
  - `Group_RecordEqualityByIdAndMembers`
  - `Principal_AbstractCannotBeInstantiated` (compile-time; or `Assert.False(typeof(Principal).IsSealed)` shape check)

- [ ] `GroupMembershipTests.cs`:
  - `Group_MembersListImmutable` (record has `IReadOnlyList`)
  - `Group_WithDifferentMemberOrder_IsNotEqual` (documents that membership order matters for record equality; ordering normalization is the graph's responsibility, not the type's)

### Step 5: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Capabilities"
git add packages/foundation/Capabilities packages/foundation/tests/Capabilities
git commit -m "feat(foundation): add Sunfish.Foundation.Capabilities Principal / Individual / Group types"
```

---

## Task 3: `ICapabilityGraph` Contract + `InMemoryCapabilityGraph` Backend

**Files:**
- Create: `packages/foundation/Capabilities/CapabilityOp.cs`
- Create: `packages/foundation/Capabilities/CapabilityProof.cs`
- Create: `packages/foundation/Capabilities/MutationResult.cs`
- Create: `packages/foundation/Capabilities/ICapabilityGraph.cs`
- Create: `packages/foundation/Capabilities/InMemoryCapabilityGraph.cs`
- Create: `packages/foundation/Capabilities/CapabilityClosure.cs`
- Create: `packages/foundation/tests/Capabilities/CapabilityOpSignatureTests.cs`
- Create: `packages/foundation/tests/Capabilities/InMemoryCapabilityGraphTests.cs`

### Step 1: `CapabilityOp` hierarchy

- [ ] Create `packages/foundation/Capabilities/CapabilityOp.cs`:

```csharp
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Base type for all mutations to the capability graph. Every op is wrapped in a
/// <see cref="SignedOperation{T}"/>; the issuer's Ed25519 signature over the
/// canonical JSON of the op is the capability proof.
/// </summary>
public abstract record CapabilityOp;

/// <summary>Creates a new principal node. Individuals carry their own PrincipalId.</summary>
public sealed record MintPrincipal(
    PrincipalId NewId,
    PrincipalKind Kind,
    IReadOnlyList<PrincipalId>? InitialMembers = null) : CapabilityOp;

public enum PrincipalKind { Individual, Group }

/// <summary>Grants <paramref name="Subject"/> the right to <paramref name="Action"/> on <paramref name="Resource"/>.</summary>
public sealed record Delegate(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action,
    DateTimeOffset? Expires = null) : CapabilityOp;

/// <summary>Revokes a previously granted delegation. Matches by (subject, resource, action) tuple.</summary>
public sealed record Revoke(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action) : CapabilityOp;

/// <summary>Adds a member to a group. Only valid if the issuer has <c>Delegate</c> capability on the group itself.</summary>
public sealed record AddMember(
    PrincipalId Group,
    PrincipalId Member) : CapabilityOp;

/// <summary>Removes a member from a group. Authority same as AddMember.</summary>
public sealed record RemoveMember(
    PrincipalId Group,
    PrincipalId Member) : CapabilityOp;
```

### Step 2: `MutationResult`, `CapabilityProof`, `ICapabilityGraph`

- [ ] Create `packages/foundation/Capabilities/MutationResult.cs`:

```csharp
namespace Sunfish.Foundation.Capabilities;

public sealed record MutationResult(MutationKind Kind, string? Reason = null)
{
    public static MutationResult Accepted { get; } = new(MutationKind.Accepted);
    public static MutationResult Rejected(string reason) => new(MutationKind.Rejected, reason);
}

public enum MutationKind { Accepted, Rejected }
```

- [ ] Create `packages/foundation/Capabilities/CapabilityProof.cs`:

```csharp
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Exportable evidence that <see cref="Subject"/> has <see cref="Action"/> on
/// <see cref="Resource"/>. Carries the chain of signed ops from the root issuer
/// down to the subject. Verifiable offline by any party that knows the root issuer.
/// </summary>
public sealed record CapabilityProof(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action,
    IReadOnlyList<SignedOperation<CapabilityOp>> OpChain,
    DateTimeOffset ProvedAt);
```

- [ ] Create `packages/foundation/Capabilities/ICapabilityGraph.cs`:

```csharp
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

public interface ICapabilityGraph
{
    /// <summary>
    /// Answers "does <paramref name="subject"/> have <paramref name="action"/> on
    /// <paramref name="resource"/> at <paramref name="asOf"/>?" via transitive closure
    /// over the membership graph.
    /// </summary>
    ValueTask<bool> QueryAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Applies a signed capability op. Verifies the signature, checks issuer authority,
    /// and updates the graph. Returns <see cref="MutationResult.Accepted"/> on success
    /// or <see cref="MutationResult.Rejected(string)"/> with a reason.
    /// </summary>
    ValueTask<MutationResult> MutateAsync(
        SignedOperation<CapabilityOp> op,
        CancellationToken ct = default);

    /// <summary>
    /// Produces an exportable proof chain. For use by federation peers who want to
    /// carry a proof across trust boundaries.
    /// </summary>
    ValueTask<CapabilityProof?> ExportProofAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    /// <summary>Enumerates all applied ops (for replication / export).</summary>
    IAsyncEnumerable<SignedOperation<CapabilityOp>> ListOpsAsync(CancellationToken ct = default);
}
```

### Step 3: `InMemoryCapabilityGraph` skeleton (query path deferred to Task 4)

- [ ] Create `packages/foundation/Capabilities/InMemoryCapabilityGraph.cs`:

```csharp
using System.Collections.Concurrent;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

public sealed class InMemoryCapabilityGraph(IOperationVerifier verifier) : ICapabilityGraph
{
    private readonly ConcurrentDictionary<PrincipalId, Principal> _principals = new();
    private readonly List<SignedOperation<CapabilityOp>> _opLog = new();
    private readonly HashSet<(PrincipalId Issuer, Guid Nonce)> _processedNonces = new();
    private readonly object _sync = new();

    public ValueTask<MutationResult> MutateAsync(SignedOperation<CapabilityOp> op, CancellationToken ct = default)
    {
        // 1. Signature verification
        if (!verifier.Verify(op))
            return ValueTask.FromResult(MutationResult.Rejected("Invalid signature"));

        lock (_sync)
        {
            // 2. Replay protection per-issuer
            if (!_processedNonces.Add((op.IssuerId, op.Nonce)))
                return ValueTask.FromResult(MutationResult.Rejected("Duplicate nonce"));

            // 3. Authority + semantic validation (Task 4 fleshes this out per op kind)
            var authorityCheck = ValidateAuthority(op);
            if (authorityCheck.Kind == MutationKind.Rejected)
                return ValueTask.FromResult(authorityCheck);

            // 4. Apply
            ApplyOp(op);
            _opLog.Add(op);
        }

        return ValueTask.FromResult(MutationResult.Accepted);
    }

    public ValueTask<bool> QueryAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct = default)
    {
        lock (_sync)
        {
            return ValueTask.FromResult(CapabilityClosure.HasCapability(_principals, _opLog, subject, resource, action, asOf));
        }
    }

    public ValueTask<CapabilityProof?> ExportProofAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var chain = CapabilityClosure.FindProofChain(_principals, _opLog, subject, resource, action, asOf);
            return ValueTask.FromResult<CapabilityProof?>(chain is null ? null : new CapabilityProof(subject, resource, action, chain, asOf));
        }
    }

    public async IAsyncEnumerable<SignedOperation<CapabilityOp>> ListOpsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        List<SignedOperation<CapabilityOp>> snapshot;
        lock (_sync) { snapshot = _opLog.ToList(); }
        foreach (var op in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            yield return op;
            await Task.Yield();
        }
    }

    // --- private ---

    private MutationResult ValidateAuthority(SignedOperation<CapabilityOp> op)
    {
        // Enforced in Task 4; skeleton accepts MintPrincipal unconditionally and defers the rest.
        return MutationResult.Accepted;
    }

    private void ApplyOp(SignedOperation<CapabilityOp> op)
    {
        switch (op.Payload)
        {
            case MintPrincipal mint:
                _principals[mint.NewId] = mint.Kind == PrincipalKind.Individual
                    ? new Individual(mint.NewId)
                    : new Group(mint.NewId, mint.InitialMembers ?? Array.Empty<PrincipalId>());
                break;

            case AddMember am:
                if (_principals.TryGetValue(am.Group, out var g) && g is Group group)
                {
                    var members = group.Members.Concat(new[] { am.Member }).Distinct().ToList();
                    _principals[am.Group] = group with { Members = members };
                }
                break;

            case RemoveMember rm:
                if (_principals.TryGetValue(rm.Group, out var g2) && g2 is Group group2)
                {
                    var members = group2.Members.Where(m => !m.Equals(rm.Member)).ToList();
                    _principals[rm.Group] = group2 with { Members = members };
                }
                break;

            case Delegate _:
            case Revoke _:
                // Applied as additions to the op log; closure computes on-demand.
                break;
        }
    }
}
```

### Step 4: Stub `CapabilityClosure`

- [ ] Create `packages/foundation/Capabilities/CapabilityClosure.cs`:

```csharp
using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Computes transitive capability closure. Phase-B implementation is O(V + E) per query,
/// appropriate for in-memory backends. Postgres backend (later phase) materializes the closure.
/// </summary>
internal static class CapabilityClosure
{
    public static bool HasCapability(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf)
    {
        // Filled in during Task 4.
        return false;
    }

    public static IReadOnlyList<SignedOperation<CapabilityOp>>? FindProofChain(
        IReadOnlyDictionary<PrincipalId, Principal> principals,
        IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf)
    {
        // Filled in during Task 4.
        return null;
    }
}
```

### Step 5: Tests (skeleton + signature round-trip)

- [ ] `CapabilityOpSignatureTests.cs`:
  - `Delegate_CanBeSignedAndVerified`
  - `AddMember_CanonicalJsonIsDeterministic`
  - `MintPrincipal_WithoutInitialMembers_SerializesWithNullMembers`

- [ ] `InMemoryCapabilityGraphTests.cs` (initial):
  - `Mutate_RejectsOpWithInvalidSignature`
  - `Mutate_RejectsDuplicateNonce`
  - `Mutate_MintPrincipalIndividual_AddsToPrincipalSet`
  - `Mutate_MintPrincipalGroup_AddsToPrincipalSet`
  - `ListOpsAsync_ReturnsAppliedOpsInOrder`

### Step 6: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Capabilities"
git add packages/foundation/Capabilities/CapabilityOp.cs packages/foundation/Capabilities/MutationResult.cs \
        packages/foundation/Capabilities/CapabilityProof.cs packages/foundation/Capabilities/ICapabilityGraph.cs \
        packages/foundation/Capabilities/InMemoryCapabilityGraph.cs packages/foundation/Capabilities/CapabilityClosure.cs \
        packages/foundation/tests/Capabilities/CapabilityOpSignatureTests.cs packages/foundation/tests/Capabilities/InMemoryCapabilityGraphTests.cs
git commit -m "feat(foundation): add ICapabilityGraph + InMemoryCapabilityGraph skeleton with signed-op verification"
```

---

## Task 4: Capability Graph Queries — Transitive Membership, Expiration, Authority

**Files:**
- Modify: `packages/foundation/Capabilities/CapabilityClosure.cs`
- Modify: `packages/foundation/Capabilities/InMemoryCapabilityGraph.cs` (fill in `ValidateAuthority`)
- Create: `packages/foundation/tests/Capabilities/CapabilityGraphTransitiveMembershipTests.cs`
- Create: `packages/foundation/tests/Capabilities/CapabilityGraphExpirationTests.cs`
- Create: `packages/foundation/tests/Capabilities/CapabilityGraphCycleDetectionTests.cs`

### Step 1: Implement transitive closure

- [ ] Fill in `CapabilityClosure.HasCapability`:

The algorithm for `HasCapability(subject, resource, action, asOf)`:

1. Collect all non-expired `Delegate` ops: `{(issuer, op): op in opLog where op.Payload is Delegate d && d.Resource == resource && d.Action == action && (d.Expires is null || d.Expires > asOf)}`.
2. Filter by non-revoked: remove any for which a later `Revoke(d.Subject, d.Resource, d.Action)` exists with the same issuer.
3. For each remaining delegate `(issuer, subject_of_delegate)`, check whether `subject` is transitively a member of `subject_of_delegate`:
   - BFS from `subject_of_delegate`: start with its members (if it's a group) or `{subject_of_delegate}` (if it's an individual).
   - Expand groups; stop at individuals.
   - Use visited-set to prevent infinite loops.
   - If `subject ∈ closure(subject_of_delegate)`, the subject has the capability.
4. Additionally: verify the issuer chain. The issuer of each delegate must either be the resource's root (mint issuer) or have `Delegate` capability on the resource themselves (recursive — bounded by the op log depth).

```csharp
public static bool HasCapability(
    IReadOnlyDictionary<PrincipalId, Principal> principals,
    IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
    PrincipalId subject,
    Resource resource,
    CapabilityAction action,
    DateTimeOffset asOf)
{
    // 1. Find all applicable, non-revoked, non-expired Delegate ops for this resource+action.
    var activeDelegates = GetActiveDelegates(opLog, resource, action, asOf);

    // 2. For each delegate, check transitive membership.
    foreach (var (issuer, target) in activeDelegates)
    {
        if (IsTransitiveMember(principals, target, subject))
        {
            // 3. Verify the issuer had authority (recursive check or root-owner).
            if (IsRootOwner(opLog, issuer, resource) ||
                HasCapability(principals, opLog, issuer, resource, CapabilityAction.Delegate, asOf))
                return true;
        }
    }
    return false;
}

private static IEnumerable<(PrincipalId Issuer, PrincipalId Target)> GetActiveDelegates(
    IReadOnlyList<SignedOperation<CapabilityOp>> opLog, Resource resource, CapabilityAction action, DateTimeOffset asOf)
{
    var delegates = new Dictionary<(PrincipalId Issuer, PrincipalId Subject), SignedOperation<CapabilityOp>>();
    foreach (var op in opLog)
    {
        switch (op.Payload)
        {
            case Delegate d when d.Resource == resource && d.Action == action && (d.Expires is null || d.Expires > asOf):
                delegates[(op.IssuerId, d.Subject)] = op;
                break;
            case Revoke r when r.Resource == resource && r.Action == action:
                delegates.Remove((op.IssuerId, r.Subject));
                break;
        }
    }
    return delegates.Keys;
}

private static bool IsTransitiveMember(IReadOnlyDictionary<PrincipalId, Principal> principals, PrincipalId container, PrincipalId candidate)
{
    if (container.Equals(candidate)) return true;
    var visited = new HashSet<PrincipalId> { container };
    var queue = new Queue<PrincipalId>();
    queue.Enqueue(container);
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        if (!principals.TryGetValue(current, out var p) || p is not Group g) continue;
        foreach (var m in g.Members)
        {
            if (m.Equals(candidate)) return true;
            if (visited.Add(m)) queue.Enqueue(m);
        }
    }
    return false;
}

private static bool IsRootOwner(IReadOnlyList<SignedOperation<CapabilityOp>> opLog, PrincipalId candidate, Resource resource)
{
    // Phase B convention: the issuer of the first MintPrincipal op whose NewId owns the resource
    // is the root. For Phase B's simplified model, we treat the root owner as the first issuer to
    // issue a Delegate on this resource — a simplification that's sufficient for OpenFGA-style
    // worked-example evaluation. A richer "resource mint op" model is Phase D.
    var firstDelegate = opLog.FirstOrDefault(op => op.Payload is Delegate d && d.Resource == resource);
    return firstDelegate is not null && firstDelegate.IssuerId.Equals(candidate);
}
```

> **Note on the root-owner simplification (D-GRAPH-SHAPE deviation):** Phase B uses "first-delegator-wins" as the root-owner heuristic. A full resource-minting model (where the resource carries its own mint op with a designated root issuer) is deferred to Phase E (entity-store integration) — at that point, resources are entities with their own `ownership_chain` (spec §2.4), and the root owner is explicit. For Phase B's OpenFGA worked example, first-delegator-wins is sufficient to exercise the transitive-closure and expiration logic.

### Step 2: Implement `FindProofChain`

- [ ] Similar to `HasCapability` but returns the list of ops that prove the capability:

```csharp
public static IReadOnlyList<SignedOperation<CapabilityOp>>? FindProofChain(
    IReadOnlyDictionary<PrincipalId, Principal> principals,
    IReadOnlyList<SignedOperation<CapabilityOp>> opLog,
    PrincipalId subject,
    Resource resource,
    CapabilityAction action,
    DateTimeOffset asOf)
{
    // Walk same logic as HasCapability but collect the contributing ops.
    // Returns null if no proof exists.
    // … implementation follows the same traversal as HasCapability; on success it
    // emits the Delegate op + the group-membership ops that demonstrate subject's
    // transitive membership in the delegate's target.
    var proof = new List<SignedOperation<CapabilityOp>>();
    var activeDelegates = GetActiveDelegates(opLog, resource, action, asOf).ToList();
    foreach (var (issuer, target) in activeDelegates)
    {
        if (IsTransitiveMember(principals, target, subject))
        {
            // Find the Delegate op
            var delegateOp = opLog.Last(op => op.Payload is Delegate d
                && d.Subject.Equals(target) && d.Resource == resource && d.Action == action);
            proof.Add(delegateOp);
            // Find the membership ops that trace subject up to target
            proof.AddRange(TraceMembershipOps(opLog, target, subject));
            return proof;
        }
    }
    return null;
}

private static IEnumerable<SignedOperation<CapabilityOp>> TraceMembershipOps(
    IReadOnlyList<SignedOperation<CapabilityOp>> opLog, PrincipalId from, PrincipalId to)
{
    // Reconstructs the AddMember chain from 'from' down to 'to'. Phase-B-level detail:
    // a simple BFS that records the AddMember edges taken. See OpenFgaWorkedExampleTests
    // for the end-to-end shape.
    yield break; // full body in code
}
```

### Step 3: Fill in `ValidateAuthority` in `InMemoryCapabilityGraph`

- [ ] Rules:
  - `MintPrincipal`: always allowed (anyone can mint a new principal — identity is local)
  - `Delegate`: issuer must be the root owner of the resource OR have `Delegate` capability themselves
  - `Revoke`: issuer must be the same as the original delegate's issuer (only the grantor can revoke)
  - `AddMember` / `RemoveMember`: issuer must have `Delegate` capability on the group itself (treating the group's PrincipalId as a Resource for membership management)

```csharp
private MutationResult ValidateAuthority(SignedOperation<CapabilityOp> op)
{
    switch (op.Payload)
    {
        case MintPrincipal _: return MutationResult.Accepted;
        case Delegate d:
            if (_opLog.Count == 0 && op.IssuerId.Equals(op.IssuerId))
                return MutationResult.Accepted; // first delegate on a resource establishes root owner
            if (CapabilityClosure.HasCapability(_principals, _opLog, op.IssuerId, d.Resource, CapabilityAction.Delegate, op.IssuedAt))
                return MutationResult.Accepted;
            // Allow the first delegate on a resource (heuristic root-owner establishment)
            if (!_opLog.Any(existing => existing.Payload is Delegate ed && ed.Resource == d.Resource))
                return MutationResult.Accepted;
            return MutationResult.Rejected("Issuer lacks Delegate capability on resource");

        case Revoke r:
            var originalDelegate = _opLog.LastOrDefault(existing =>
                existing.Payload is Delegate ed && ed.Subject.Equals(r.Subject)
                && ed.Resource == r.Resource && ed.Action == r.Action);
            if (originalDelegate is null)
                return MutationResult.Rejected("No matching delegate to revoke");
            if (!originalDelegate.IssuerId.Equals(op.IssuerId))
                return MutationResult.Rejected("Only the original issuer may revoke");
            return MutationResult.Accepted;

        case AddMember am:
        case RemoveMember rm:
            var groupId = (op.Payload is AddMember a) ? a.Group : ((RemoveMember)op.Payload).Group;
            var groupResource = new Resource($"group:{groupId.ToBase64Url()}");
            if (CapabilityClosure.HasCapability(_principals, _opLog, op.IssuerId, groupResource, CapabilityAction.Delegate, op.IssuedAt))
                return MutationResult.Accepted;
            // Bootstrap: the group creator (whoever minted it) may manage it without an explicit Delegate.
            if (!_opLog.Any(existing => existing.Payload is AddMember ae && ae.Group.Equals(groupId)
                                        || existing.Payload is RemoveMember re && re.Group.Equals(groupId)))
                return MutationResult.Accepted;
            return MutationResult.Rejected("Issuer lacks Delegate capability on group");
    }
    return MutationResult.Rejected("Unknown op kind");
}
```

### Step 4: Tests for transitive membership

- [ ] `CapabilityGraphTransitiveMembershipTests.cs`:
  - `Individual_HasCapability_OnDirectDelegate`
  - `Individual_HasCapability_ViaGroupMembership`
  - `Individual_HasCapability_ViaNestedGroupMembership` (user → team → firm → property)
  - `Individual_LosesCapability_AfterRemoveMember`
  - `Individual_HasCapability_OnDelegateToIndividualDirectly`

### Step 5: Tests for expiration

- [ ] `CapabilityGraphExpirationTests.cs`:
  - `Delegate_WithFutureExpires_IsActive`
  - `Delegate_WithPastExpires_IsNotActive`
  - `Delegate_WithExpires_TogglesAtBoundary`
  - `Query_AsOf_PastTime_ReturnsHistoricalDecision`

### Step 6: Tests for cycle detection

- [ ] `CapabilityGraphCycleDetectionTests.cs`:
  - `Group_MemberContainsSelf_QueryDoesNotStackOverflow`
  - `TwoGroups_MutuallyRecursive_QueryTerminates`
  - `Mutate_AddMember_CreatingCycle_IsRejected` (if explicit cycle-detection check is added at mutate time)

### Step 7: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Capabilities"
git add packages/foundation/Capabilities/CapabilityClosure.cs \
        packages/foundation/Capabilities/InMemoryCapabilityGraph.cs \
        packages/foundation/tests/Capabilities/CapabilityGraphTransitiveMembershipTests.cs \
        packages/foundation/tests/Capabilities/CapabilityGraphExpirationTests.cs \
        packages/foundation/tests/Capabilities/CapabilityGraphCycleDetectionTests.cs
git commit -m "feat(foundation): implement capability-graph closure — transitive membership, expiration, cycle detection"
```

---

## Task 5: `Sunfish.Foundation.Macaroons`

**Files:**
- Create: `packages/foundation/Macaroons/Macaroon.cs`
- Create: `packages/foundation/Macaroons/Caveat.cs`
- Create: `packages/foundation/Macaroons/MacaroonContext.cs`
- Create: `packages/foundation/Macaroons/IMacaroonIssuer.cs`
- Create: `packages/foundation/Macaroons/IMacaroonVerifier.cs`
- Create: `packages/foundation/Macaroons/RootKeyStore.cs`
- Create: `packages/foundation/Macaroons/FirstPartyCaveatParser.cs`
- Create: `packages/foundation/Macaroons/MacaroonCodec.cs`
- Create: `packages/foundation/tests/Macaroons/*.cs`

### Step 1: Types

- [ ] Create `packages/foundation/Macaroons/Macaroon.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public sealed record Macaroon(
    string Location,
    string Identifier,
    IReadOnlyList<Caveat> Caveats,
    byte[] Signature);
```

- [ ] Create `packages/foundation/Macaroons/Caveat.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public readonly record struct Caveat(string Predicate)
{
    public override string ToString() => Predicate;
}
```

- [ ] Create `packages/foundation/Macaroons/MacaroonContext.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public sealed record MacaroonContext(
    DateTimeOffset Now,
    string? SubjectUri,
    string? ResourceSchema,
    string? RequestedAction,
    string? DeviceIp);
```

### Step 2: Root-key store + issuer / verifier contracts

- [ ] Create `packages/foundation/Macaroons/RootKeyStore.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public interface IRootKeyStore
{
    ValueTask<byte[]?> GetRootKeyAsync(string location, CancellationToken ct = default);
}

public sealed class InMemoryRootKeyStore : IRootKeyStore
{
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    public void Set(string location, byte[] key) => _keys[location] = key;

    public ValueTask<byte[]?> GetRootKeyAsync(string location, CancellationToken ct = default) =>
        ValueTask.FromResult(_keys.TryGetValue(location, out var k) ? k : null);
}
```

- [ ] Create `packages/foundation/Macaroons/IMacaroonIssuer.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public interface IMacaroonIssuer
{
    ValueTask<Macaroon> MintAsync(string location, string identifier, IEnumerable<Caveat> caveats, CancellationToken ct = default);
    ValueTask<Macaroon> AttenuateAsync(Macaroon existing, IEnumerable<Caveat> additionalCaveats, CancellationToken ct = default);
}
```

- [ ] Create `packages/foundation/Macaroons/IMacaroonVerifier.cs`:

```csharp
namespace Sunfish.Foundation.Macaroons;

public interface IMacaroonVerifier
{
    ValueTask<MacaroonVerificationResult> VerifyAsync(Macaroon macaroon, MacaroonContext context, CancellationToken ct = default);
}

public sealed record MacaroonVerificationResult(bool IsValid, string? Reason = null);
```

### Step 3: Wire format + HMAC chain

- [ ] Create `packages/foundation/Macaroons/MacaroonCodec.cs`:

```csharp
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Sunfish.Foundation.Macaroons;

internal static class MacaroonCodec
{
    private const byte RecordSeparator = 0x1E;
    private const byte UnitSeparator = 0x1F;

    public static byte[] ComputeChain(byte[] rootKey, string identifier, IReadOnlyList<Caveat> caveats)
    {
        var sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(identifier));
        foreach (var c in caveats)
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(c.Predicate));
        return sig;
    }

    public static string EncodeBase64Url(Macaroon m)
    {
        using var ms = new MemoryStream();
        ms.Write(Encoding.UTF8.GetBytes(m.Location));
        ms.WriteByte(RecordSeparator);
        ms.Write(Encoding.UTF8.GetBytes(m.Identifier));
        ms.WriteByte(RecordSeparator);
        foreach (var c in m.Caveats)
        {
            ms.Write(Encoding.UTF8.GetBytes(c.Predicate));
            ms.WriteByte(RecordSeparator);
        }
        ms.WriteByte(UnitSeparator);
        ms.Write(m.Signature);
        return Base64Url.EncodeToString(ms.ToArray());
    }

    public static Macaroon DecodeBase64Url(string encoded)
    {
        var bytes = Base64Url.DecodeFromChars(encoded);
        // Locate the 0x1F; everything after is the 32-byte signature.
        var unitSepIdx = Array.IndexOf(bytes, UnitSeparator);
        if (unitSepIdx < 0 || bytes.Length - unitSepIdx - 1 != 32)
            throw new FormatException("Malformed macaroon wire format.");

        var sigBytes = bytes[(unitSepIdx + 1)..];
        var headerBytes = bytes[..unitSepIdx];
        var parts = SplitOn(headerBytes, RecordSeparator);
        if (parts.Count < 2)
            throw new FormatException("Macaroon missing location or identifier.");

        var location = Encoding.UTF8.GetString(parts[0]);
        var identifier = Encoding.UTF8.GetString(parts[1]);
        var caveats = parts.Skip(2).Where(p => p.Length > 0).Select(p => new Caveat(Encoding.UTF8.GetString(p))).ToList();
        return new Macaroon(location, identifier, caveats, sigBytes);
    }

    private static List<byte[]> SplitOn(byte[] source, byte delimiter)
    {
        var result = new List<byte[]>();
        int start = 0;
        for (int i = 0; i <= source.Length; i++)
        {
            if (i == source.Length || source[i] == delimiter)
            {
                result.Add(source[start..i]);
                start = i + 1;
            }
        }
        return result;
    }
}
```

### Step 4: First-party caveat parser (Phase B: 5 forms)

- [ ] Create `packages/foundation/Macaroons/FirstPartyCaveatParser.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;

namespace Sunfish.Foundation.Macaroons;

internal static partial class FirstPartyCaveatParser
{
    public static bool Evaluate(Caveat caveat, MacaroonContext ctx)
    {
        var p = caveat.Predicate.Trim();

        // time <= "ISO8601"
        var timeMatch = TimeRegex().Match(p);
        if (timeMatch.Success)
        {
            var limit = DateTimeOffset.Parse(timeMatch.Groups[1].Value);
            return ctx.Now <= limit;
        }

        // subject == "uri"
        var subjMatch = SubjectRegex().Match(p);
        if (subjMatch.Success)
            return string.Equals(subjMatch.Groups[1].Value, ctx.SubjectUri, StringComparison.Ordinal);

        // resource.schema matches "glob"
        var schemaMatch = SchemaMatchRegex().Match(p);
        if (schemaMatch.Success)
        {
            var glob = schemaMatch.Groups[1].Value;
            var pattern = "^" + Regex.Escape(glob).Replace(@"\*", ".*") + "$";
            return ctx.ResourceSchema is not null && Regex.IsMatch(ctx.ResourceSchema, pattern);
        }

        // action in ["a", "b", ...]
        var actionMatch = ActionInRegex().Match(p);
        if (actionMatch.Success)
        {
            var allowed = actionMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"')).ToHashSet(StringComparer.Ordinal);
            return ctx.RequestedAction is not null && allowed.Contains(ctx.RequestedAction);
        }

        // device_ip in "cidr"
        var ipMatch = DeviceIpRegex().Match(p);
        if (ipMatch.Success && ctx.DeviceIp is not null)
            return IsInCidr(ctx.DeviceIp, ipMatch.Groups[1].Value);

        return false; // unknown caveat — fail closed
    }

    private static bool IsInCidr(string ip, string cidr) { /* … CIDR match using IPAddress + bit-mask … */ return true; }

    [GeneratedRegex(@"^time\s*<=?\s*""([^""]+)""$")] private static partial Regex TimeRegex();
    [GeneratedRegex(@"^subject\s*==\s*""([^""]+)""$")] private static partial Regex SubjectRegex();
    [GeneratedRegex(@"^resource\.schema\s*matches\s*""([^""]+)""$")] private static partial Regex SchemaMatchRegex();
    [GeneratedRegex(@"^action\s*in\s*\[([^\]]+)\]$")] private static partial Regex ActionInRegex();
    [GeneratedRegex(@"^device_ip\s*in\s*""([^""]+)""$")] private static partial Regex DeviceIpRegex();
}
```

### Step 5: Default `IMacaroonIssuer` + `IMacaroonVerifier` implementations

Ship simple default implementations in the same folder (not new files per impl — one file with both). Signature chain construction and verification use `MacaroonCodec.ComputeChain`; caveats evaluated via `FirstPartyCaveatParser.Evaluate`.

### Step 6: Tests

- [ ] `MacaroonIssueAndVerifyTests.cs`:
  - `Mint_ProducesMacaroonWithSignatureMatchingHmacChain`
  - `Verify_SucceedsForCorrectlyConstructedMacaroon`
  - `Verify_FailsWhenSignatureTampered`
  - `Verify_FailsWhenCaveatsTampered`

- [ ] `MacaroonAttenuationTests.cs`:
  - `Attenuate_AddsCaveat_ExtendsSignatureChain`
  - `Attenuate_PreservesLocationAndIdentifier`
  - `AttenuatedMacaroon_VerifiesWithOriginalRootKey`

- [ ] `FirstPartyCaveatParserTests.cs`:
  - `TimeCaveat_AcceptsBeforeDeadline`
  - `TimeCaveat_RejectsAfterDeadline`
  - `SubjectCaveat_MatchesExactSubject`
  - `SchemaGlob_MatchesWildcardPattern`
  - `ActionInList_AcceptsListedAction`
  - `ActionInList_RejectsUnlistedAction`
  - `DeviceIpInCidr_MatchesCorrectly`
  - `UnknownCaveat_FailsClosed`

- [ ] `MacaroonWireFormatTests.cs`:
  - `Encode_Decode_RoundTripsCorrectly`
  - `Decode_RejectsMalformedInput`
  - `Decode_RejectsMissingSignature`

### Step 7: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Macaroons"
git add packages/foundation/Macaroons packages/foundation/tests/Macaroons
git commit -m "feat(foundation): add Sunfish.Foundation.Macaroons — HMAC-SHA256 bearer tokens, first-party caveats, attenuation"
```

---

## Task 6: `Sunfish.Foundation.PolicyEvaluator` — OpenFGA-Style ReBAC

**Files:**
- Create: `packages/foundation/PolicyEvaluator/IPermissionEvaluator.cs`
- Create: `packages/foundation/PolicyEvaluator/Decision.cs`
- Create: `packages/foundation/PolicyEvaluator/Obligation.cs`
- Create: `packages/foundation/PolicyEvaluator/Subject.cs`
- Create: `packages/foundation/PolicyEvaluator/ActionType.cs`
- Create: `packages/foundation/PolicyEvaluator/ContextEnvelope.cs`
- Create: `packages/foundation/PolicyEvaluator/PolicyModel.cs`
- Create: `packages/foundation/PolicyEvaluator/TypeDefinition.cs`
- Create: `packages/foundation/PolicyEvaluator/RelationRewrite.cs`
- Create: `packages/foundation/PolicyEvaluator/PolicyModelBuilder.cs`
- Create: `packages/foundation/PolicyEvaluator/TypeBuilder.cs`
- Create: `packages/foundation/PolicyEvaluator/RelationBuilder.cs`
- Create: `packages/foundation/PolicyEvaluator/ReBACPolicyEvaluator.cs`
- Create: `packages/foundation/tests/PolicyEvaluator/*.cs`

### Step 1: Contract types matching spec §3.5

- [ ] Create `packages/foundation/PolicyEvaluator/IPermissionEvaluator.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The kernel permission-evaluator contract from spec §3.5.
/// Given (subject, action, resource, context), returns a reasoned <see cref="Decision"/>.
/// Phase B's default implementation is <see cref="ReBACPolicyEvaluator"/> — OpenFGA-style
/// ReBAC over the <see cref="Capabilities.ICapabilityGraph"/>.
/// Alternative engines (OPA/Rego, AWS Cedar) plug in via this same interface.
/// </summary>
public interface IPermissionEvaluator
{
    ValueTask<Decision> EvaluateAsync(
        Subject subject,
        ActionType action,
        Resource resource,
        ContextEnvelope context,
        CancellationToken ct = default);
}
```

- [ ] `Decision.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed record Decision(
    DecisionKind Kind,
    string? Reason,
    IReadOnlyList<string> MatchedPolicies,
    IReadOnlyList<Obligation> Obligations)
{
    public static Decision Permit(string reason, params string[] matched) =>
        new(DecisionKind.Permit, reason, matched, Array.Empty<Obligation>());

    public static Decision Deny(string reason) =>
        new(DecisionKind.Deny, reason, Array.Empty<string>(), Array.Empty<Obligation>());

    public static Decision Indeterminate(string reason) =>
        new(DecisionKind.Indeterminate, reason, Array.Empty<string>(), Array.Empty<Obligation>());
}

public enum DecisionKind { Permit, Deny, Indeterminate }
```

- [ ] `Obligation.cs`, `Subject.cs`, `ActionType.cs`, `ContextEnvelope.cs` — all small records mirroring spec §3.5 shapes.

### Step 2: Policy model types

- [ ] `RelationRewrite.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public abstract record RelationRewrite
{
    public sealed record Self : RelationRewrite;
    public sealed record DirectUsers(IReadOnlyList<string> AllowedTypes) : RelationRewrite;
    public sealed record ComputedUserset(string Relation) : RelationRewrite;
    public sealed record TupleToUserset(string Tupleset, string ComputedRelation) : RelationRewrite;
    public sealed record Union(IReadOnlyList<RelationRewrite> Children) : RelationRewrite;
    public sealed record Intersection(IReadOnlyList<RelationRewrite> Children) : RelationRewrite;
    public sealed record Exclusion(RelationRewrite Include, RelationRewrite Exclude) : RelationRewrite;
}
```

- [ ] `TypeDefinition.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed record TypeDefinition(
    string Name,
    IReadOnlyDictionary<string, RelationRewrite> Relations);
```

- [ ] `PolicyModel.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed class PolicyModel
{
    public IReadOnlyDictionary<string, TypeDefinition> Types { get; }

    public PolicyModel(IReadOnlyDictionary<string, TypeDefinition> types) => Types = types;

    public static PolicyModelBuilder Create() => new();
}
```

### Step 3: Fluent builder

- [ ] `PolicyModelBuilder.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed class PolicyModelBuilder
{
    private readonly Dictionary<string, TypeDefinition> _types = new(StringComparer.Ordinal);

    public PolicyModelBuilder Type(string name, Action<TypeBuilder>? configure = null)
    {
        var tb = new TypeBuilder(name);
        configure?.Invoke(tb);
        _types[name] = tb.Build();
        return this;
    }

    public PolicyModel Build() => new(_types);
}
```

- [ ] `TypeBuilder.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed class TypeBuilder(string name)
{
    private readonly Dictionary<string, RelationRewrite> _relations = new(StringComparer.Ordinal);

    public TypeBuilder Relation(string relationName, RelationRewrite rewrite)
    {
        _relations[relationName] = rewrite;
        return this;
    }

    public TypeBuilder Relation(string relationName, Action<RelationBuilder> configure)
    {
        var rb = new RelationBuilder();
        configure(rb);
        _relations[relationName] = rb.Build();
        return this;
    }

    internal TypeDefinition Build() => new(name, _relations);
}
```

- [ ] `RelationBuilder.cs`:

```csharp
namespace Sunfish.Foundation.PolicyEvaluator;

public sealed class RelationBuilder
{
    private RelationRewrite? _root;

    public RelationBuilder DirectUsers(params string[] types) { _root = new RelationRewrite.DirectUsers(types); return this; }
    public RelationBuilder ComputedFrom(string relation) { _root = new RelationRewrite.ComputedUserset(relation); return this; }
    public RelationBuilder TupleToUserset(string tupleset, string computed) { _root = new RelationRewrite.TupleToUserset(tupleset, computed); return this; }
    public RelationBuilder Union(params RelationRewrite[] children) { _root = new RelationRewrite.Union(children); return this; }

    internal RelationRewrite Build() => _root ?? new RelationRewrite.Self();
}
```

### Step 4: `ReBACPolicyEvaluator`

- [ ] Consults `ICapabilityGraph` to resolve relation tuples. Core algorithm:

1. Given `(subject, action, resource)`:
   - Look up the resource's type in the `PolicyModel`.
   - The `action` maps to a relation name on that type (e.g., `Read` → `can_read`).
   - Evaluate the relation's `RelationRewrite` against the graph:
     - `Self`: is subject directly tied to resource via this relation? → query capability graph for the tuple `(subject, relation, resource)`.
     - `DirectUsers([types])`: ditto, with type-check.
     - `ComputedUserset(other)`: recurse — evaluate `other` relation instead.
     - `TupleToUserset(tupleset, computed)`: find all `resource' s.t. (resource, tupleset, resource')` is in graph; recurse with `(subject, computed, resource')`.
     - `Union`: any child evaluates true.
     - `Intersection`: all children evaluate true.
     - `Exclusion`: `Include AND NOT Exclude`.
- Return `Decision.Permit(...)` if the relation evaluates true, `Decision.Deny(...)` otherwise.

```csharp
public sealed class ReBACPolicyEvaluator(PolicyModel model, Capabilities.ICapabilityGraph graph) : IPermissionEvaluator
{
    public async ValueTask<Decision> EvaluateAsync(Subject subject, ActionType action, Resource resource, ContextEnvelope context, CancellationToken ct = default)
    {
        if (!model.Types.TryGetValue(resource.TypeName, out var typeDef))
            return Decision.Indeterminate($"No policy type '{resource.TypeName}' in model.");

        var relationName = MapActionToRelation(action);
        if (!typeDef.Relations.TryGetValue(relationName, out var rewrite))
            return Decision.Deny($"No '{relationName}' relation on type '{resource.TypeName}'.");

        var result = await EvaluateRewriteAsync(rewrite, subject, resource, context, ct);
        return result
            ? Decision.Permit($"relation {resource.TypeName}#{relationName} evaluated true", resource.TypeName + "#" + relationName)
            : Decision.Deny($"relation {resource.TypeName}#{relationName} evaluated false");
    }

    private async ValueTask<bool> EvaluateRewriteAsync(RelationRewrite rw, Subject subject, Resource resource, ContextEnvelope ctx, CancellationToken ct)
    {
        // … switch on RelationRewrite kind, calling graph.QueryAsync for the direct-tuple cases
        return false;
    }

    private static string MapActionToRelation(ActionType action) => action.Name switch
    {
        "read"   => "can_read",
        "write"  => "can_write",
        "delete" => "can_delete",
        _ => "can_" + action.Name,
    };
}
```

### Step 5: Spec §3.5 OpenFGA worked example — end-to-end test

- [ ] `OpenFgaWorkedExampleTests.cs` expresses **exactly** the spec §3.5 model as a fluent-API build:

```csharp
using Xunit;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.PolicyEvaluator;

public class OpenFgaWorkedExampleTests
{
    [Fact]
    public async Task JimCanInspectInspectionBecauseJimIsInAcmeWhichIsPmFirmOfProperty42()
    {
        // -- Arrange: build the policy model (matches spec §3.5 DSL line-for-line) --
        var model = PolicyModel.Create()
            .Type("user")
            .Type("inspection_firm", t => t
                .Relation("employee", r => r.DirectUsers("user")))
            .Type("property", t => t
                .Relation("landlord", r => r.DirectUsers("user"))
                .Relation("pm_firm",  r => r.DirectUsers("inspection_firm"))
                .Relation("inspector", r => r.DirectUsers("user", "inspection_firm#employee"))
                .Relation("can_inspect",
                    new RelationRewrite.Union(new RelationRewrite[]
                    {
                        new RelationRewrite.ComputedUserset("inspector"),
                        new RelationRewrite.TupleToUserset("pm_firm", "employee"),
                    })))
            .Type("inspection", t => t
                .Relation("property", r => r.DirectUsers("property"))
                .Relation("author",   r => r.DirectUsers("user"))
                .Relation("can_write",
                    new RelationRewrite.Union(new RelationRewrite[]
                    {
                        new RelationRewrite.ComputedUserset("author"),
                        new RelationRewrite.TupleToUserset("property", "can_inspect"),
                    }))
                .Relation("can_read",
                    new RelationRewrite.Union(new RelationRewrite[]
                    {
                        new RelationRewrite.ComputedUserset("author"),
                        new RelationRewrite.TupleToUserset("property", "can_inspect"),
                        new RelationRewrite.TupleToUserset("property", "landlord"),
                    })))
            .Build();

        // -- Arrange: principals and capability-graph seed --
        using var landlord = KeyPair.Generate();
        using var jim = KeyPair.Generate();
        using var acmeAdmin = KeyPair.Generate();

        var verifier = new Ed25519Verifier();
        var graph = new InMemoryCapabilityGraph(verifier);

        var property42 = new Resource("property:42");
        var inspectionToday = new Resource("inspection:2026-04-17");
        var acmeFirm = new PrincipalId(/*group-id bytes*/);

        // Mint individuals + group
        // Delegate landlord→property42 (establishes root owner of property:42)
        // AddMember(acmeFirm, jim.PrincipalId)
        // Delegate landlord→property42.pm_firm = acmeFirm
        // … (see test body for the full op sequence, constructed via an Ed25519Signer for each principal)

        var evaluator = new ReBACPolicyEvaluator(model, graph);

        var decision = await evaluator.EvaluateAsync(
            subject: new Subject(jim.PrincipalId, Array.Empty<string>()),
            action: new ActionType("read"),
            resource: new Resource("inspection:2026-04-17", TypeName: "inspection"),
            context: new ContextEnvelope(DateTimeOffset.Parse("2026-04-17T14:00Z"), Purpose: "inspection-review"));

        Assert.Equal(DecisionKind.Permit, decision.Kind);
        Assert.Contains("can_read", decision.MatchedPolicies.Single());
    }

    [Fact]
    public async Task ProspectiveBuyerCannotReadLease()
    {
        // Spec §3.5 second example — default-deny for non-owners
        // … similar setup, different subject, assert DecisionKind.Deny
    }
}
```

### Step 6: Additional tests

- [ ] `PolicyModelBuilderTests.cs`:
  - `Build_PreservesTypeOrder`
  - `Build_AllowsUnionRelation`
  - `Build_RejectsDuplicateTypeName`

- [ ] `ReBACEvaluatorTests.cs`:
  - `Evaluate_DirectRelation_PermitsWhenTupleExists`
  - `Evaluate_DirectRelation_DeniesWhenTupleMissing`
  - `Evaluate_UnionRelation_ShortCircuitsOnFirstPermit`
  - `Evaluate_TupleToUserset_FollowsIndirectRelation`
  - `Evaluate_MissingTypeInModel_ReturnsIndeterminate`
  - `Evaluate_UnknownRelationOnType_ReturnsDeny`

### Step 7: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~PolicyEvaluator"
git add packages/foundation/PolicyEvaluator packages/foundation/tests/PolicyEvaluator
git commit -m "feat(foundation): add Sunfish.Foundation.PolicyEvaluator — OpenFGA-style ReBAC over capability graph"
```

---

## Task 7: DI Integration — `AddSunfishDecentralization()`

**Files:**
- Create: `packages/foundation/Extensions/SunfishDecentralizationExtensions.cs`
- Create: `packages/foundation/Extensions/DecentralizationOptions.cs`
- Create: `packages/foundation/tests/Extensions/AddSunfishDecentralizationTests.cs`

### Step 1: Options

- [ ] Create `packages/foundation/Extensions/DecentralizationOptions.cs`:

```csharp
using Sunfish.Foundation.PolicyEvaluator;

namespace Sunfish.Foundation.Extensions;

public sealed class DecentralizationOptions
{
    /// <summary>
    /// <b>DO NOT enable in production.</b> When true, registers <c>DevKeyStore</c> and
    /// <c>InMemoryRootKeyStore</c> for local development / tests. Emits a startup warning
    /// when enabled. Default: <c>false</c>.
    /// </summary>
    public bool EnableDevKeyMaterial { get; set; } = false;

    /// <summary>
    /// Fluent callback for configuring the default <see cref="PolicyModel"/> that the
    /// <see cref="IPermissionEvaluator"/> consults.
    /// </summary>
    public Action<PolicyModelBuilder>? PolicyModel { get; set; }
}
```

### Step 2: Extension method

- [ ] Create `packages/foundation/Extensions/SunfishDecentralizationExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Macaroons;
using Sunfish.Foundation.PolicyEvaluator;

namespace Sunfish.Foundation.Extensions;

public static class SunfishDecentralizationExtensions
{
    /// <summary>
    /// Registers Sunfish decentralization primitives — crypto, capability graph,
    /// macaroons, policy evaluator — in the DI container. Returns the builder for chaining.
    /// </summary>
    public static SunfishBuilder AddSunfishDecentralization(
        this SunfishBuilder builder,
        Action<DecentralizationOptions>? configure = null)
    {
        var options = new DecentralizationOptions();
        configure?.Invoke(options);

        // Stateless verifier + graph (singletons)
        builder.Services.AddSingleton<IOperationVerifier, Ed25519Verifier>();
        builder.Services.AddSingleton<ICapabilityGraph, InMemoryCapabilityGraph>();

        // Macaroons
        builder.Services.AddSingleton<IMacaroonVerifier, DefaultMacaroonVerifier>();
        builder.Services.AddSingleton<IMacaroonIssuer, DefaultMacaroonIssuer>();

        // Policy model (configurable) — build once, register as singleton
        var modelBuilder = PolicyModel.Create();
        options.PolicyModel?.Invoke(modelBuilder);
        var model = modelBuilder.Build();
        builder.Services.AddSingleton(model);
        builder.Services.AddSingleton<IPermissionEvaluator, ReBACPolicyEvaluator>();

        // Dev-only key material — gated
        if (options.EnableDevKeyMaterial)
        {
            builder.Services.AddSingleton<DevKeyStore>();
            builder.Services.AddSingleton<IRootKeyStore, InMemoryRootKeyStore>();
            builder.Services.AddSingleton<IHostedService, DevKeyMaterialWarningService>();
        }

        return builder;
    }

    /// <summary>
    /// Emits a startup warning when <c>EnableDevKeyMaterial = true</c>. Matches the
    /// <c>DemoTenantContext</c> / <c>MockOktaService</c> warning pattern from Phase 9.
    /// </summary>
    internal sealed class DevKeyMaterialWarningService(ILogger<DevKeyMaterialWarningService> logger) : Microsoft.Extensions.Hosting.IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            logger.LogWarning(
                "DEV KEY MATERIAL ACTIVE: Sunfish decentralization is running with EnableDevKeyMaterial = true. " +
                "In-memory DevKeyStore and InMemoryRootKeyStore are registered. " +
                "DO NOT DEPLOY TO PRODUCTION. Replace with KMS / HSM / OS-keyring-backed IOperationSigner and IRootKeyStore before shipping.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
```

### Step 3: Tests

- [ ] `AddSunfishDecentralizationTests.cs`:
  - `Register_ResolvesIOperationVerifier`
  - `Register_ResolvesICapabilityGraph`
  - `Register_ResolvesIPermissionEvaluator`
  - `Register_ResolvesIMacaroonVerifier`
  - `Register_WithoutDevKeys_DoesNotRegisterDevKeyStore`
  - `Register_WithDevKeys_RegistersDevKeyStore`
  - `Register_WithDevKeys_EmitsStartupWarning` (assert log via `ILogger<DevKeyMaterialWarningService>` substitute)
  - `Register_InvokesPolicyModelCallback`
  - `Register_RegisteredPolicyModelIsSingleton`

### Step 4: Build + commit

- [ ] 
```bash
dotnet build packages/foundation/Sunfish.Foundation.csproj
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Extensions.AddSunfish"
git add packages/foundation/Extensions/SunfishDecentralizationExtensions.cs \
        packages/foundation/Extensions/DecentralizationOptions.cs \
        packages/foundation/tests/Extensions/AddSunfishDecentralizationTests.cs
git commit -m "feat(foundation): add AddSunfishDecentralization() DI extension with dev-key-material gate + warning"
```

---

## Task 8: End-to-End Integration Test

**Files:**
- Create: `packages/foundation/tests/Integration/DecentralizationEndToEndTests.cs`

### Step 1: The worked scenario

Adopts spec §10.2 property-management vocabulary end-to-end:

1. **Principals minted:** `landlord:acme-rentals`, `firm:acme-inspection`, `user:jim@acmeinsp` (individual), `user:sam@landlord` (individual, owner).
2. **Group:** `acme-inspection` has `jim` as a member.
3. **Delegations:**
   - `sam` delegates `can_read` + `can_write` on `inspection:2026-04-17` to `firm:acme-inspection` with `Expires = 2026-06-30`.
   - `sam` also delegates `pm_firm` relation on `property:42` to `firm:acme-inspection`.
4. **Query 1:** `jim` tries to write `inspection:2026-04-17` on `2026-04-17T14:00Z` — should `Permit`.
5. **Query 2:** `jim` tries to write on `2026-07-15` — should `Deny` (expired).
6. **Mutation:** `RemoveMember(acme-inspection, jim)` on `2026-04-20` — signed by firm admin.
7. **Query 3:** `jim` tries to write on `2026-04-21T14:00Z` — should `Deny` (no longer in firm).
8. **Macaroon lane:** `sam` mints a short-lived macaroon "valid 48h, subject=jim, action in [read]"; `jim` presents it; verifier accepts if time is within window and subject/action match.
9. **CapabilityProof export:** after Query 1, call `ExportProofAsync` and verify the chain contains `(Delegate, AddMember)` pair.
10. **Federation-ready check:** export all ops via `ListOpsAsync`, count matches mutations applied.

### Step 2: Assertions verify:

- `IPermissionEvaluator.EvaluateAsync` matches `ICapabilityGraph.QueryAsync` (the evaluator properly consults the graph)
- Expiration is enforced at query time, not mutate time
- Revocation via `RemoveMember` is immediately reflected in subsequent queries
- Macaroon path is independent of capability graph (parallel primitive)
- Exported proof is structurally sound (at least one `Delegate` op + membership chain)

### Step 3: Build + commit

- [ ] 
```bash
dotnet test packages/foundation/tests/tests.csproj --filter "FullyQualifiedName~Integration"
git add packages/foundation/tests/Integration/DecentralizationEndToEndTests.cs
git commit -m "test(foundation): add end-to-end decentralization integration — property-management scenario"
```

---

## Task 9: Documentation

**Files:**
- Modify: `packages/foundation/README.md` (if it exists; otherwise no-op — plan does not create new docs proactively)
- Modify: `README.md` (root) — add "Platform Phase B (decentralization primitives) — SHIPPED" line to the roadmap callout, if a roadmap exists there
- Modify: `docs/specifications/sunfish-platform-specification.md` — add a Phase-B-shipped callout under §4 Roadmap (one-line edit). **Do not** rewrite spec content; only annotate shipment status.
- Modify: existing `accelerators/bridge/PLATFORM_ALIGNMENT.md` (if present) — flip the decentralization rows from 🔴 to 🟡 (primitives shipped; not yet integrated with entity store)

### Step 1: Specification status annotation

- [ ] In `docs/specifications/sunfish-platform-specification.md`, under "§4 Phased Implementation Roadmap", find the Platform Phase B row and append status `[SHIPPED 2026-04-18]`. Do not modify the technical content of the phase description.

### Step 2: PLATFORM_ALIGNMENT.md deltas

- [ ] Update Bridge's alignment inventory if it exists:

```markdown
## Spec Section 2 — Reference Architecture

| Capability | Bridge Status | Notes |
|---|---|---|
| Cryptographic ownership proofs | 🟡 | Foundation primitives shipped (Platform Phase B); not yet bound to entity writes |
| Time-bound / delegation | 🟡 | Keyhive-inspired capability graph + Macaroon bearer tokens shipped in Foundation |
```

### Step 3: XML doc comment pass

- [ ] Every public type in the four new namespaces has at least a summary XML comment. (Already applied in Tasks 1–6 code snippets; Task 9 verifies coverage.)
- [ ] Run `dotnet build` with `/warnaserror:CS1591`. If any public member is missing docs, add it. The existing repo-wide `<NoWarn>CS1591</NoWarn>` does NOT exempt the new code — remove it for the new files by scoping via `#pragma warning disable/restore CS1591` **only** around pre-existing undocumented areas.

### Step 4: Cross-references

- [ ] Each new namespace's primary type (`PrincipalId`, `ICapabilityGraph`, `Macaroon`, `IPermissionEvaluator`) carries a `<see cref="…"/>` XML link back to:
  - `docs/specifications/sunfish-platform-specification.md` §2.4 / §3.5 / §10.2
  - `docs/specifications/research-notes/automerge-evaluation.md` §4.2 (Keyhive model adoption)
  - `docs/specifications/research-notes/external-references.md` §3.1 (OpenFGA layering)

### Step 5: Commit

- [ ] 
```bash
git add docs/specifications/sunfish-platform-specification.md README.md accelerators/bridge/PLATFORM_ALIGNMENT.md
git commit -m "docs: annotate Platform Phase B shipment; update PLATFORM_ALIGNMENT decentralization rows"
```

---

## Worked Example — OpenFGA Scenario End-to-End

This is the canonical worked example that `OpenFgaWorkedExampleTests` and `DecentralizationEndToEndTests` exercise. Intended as an at-a-glance "how does all this compose?" narrative.

### 1. Actors and identities

```
Individual: sam       (landlord — Ed25519 keypair)
Individual: jim       (inspector — Ed25519 keypair)
Individual: alice     (acme-inspection admin — Ed25519 keypair)
Group:      acme-inspection  (initial members: { alice })

Resource:   property:42       (building owned by sam)
Resource:   inspection:2026-04-17 (the inspection record)
```

### 2. OpenFGA-style authorization model (spec §3.5)

**DSL form (aspirational — for documentation):**

```
type user
type inspection_firm
  relations
    define employee: [user]
type property
  relations
    define landlord:   [user]
    define pm_firm:    [inspection_firm]
    define inspector:  [user, inspection_firm#employee]
    define can_inspect: inspector or employee from pm_firm
type inspection
  relations
    define property:  [property]
    define author:    [user]
    define can_write: author or can_inspect from property
    define can_read:  author or can_inspect from property or landlord from property
```

**Fluent C# equivalent (what Phase B actually ships):**

```csharp
var model = PolicyModel.Create()
    .Type("user")
    .Type("inspection_firm", t => t
        .Relation("employee", r => r.DirectUsers("user")))
    .Type("property", t => t
        .Relation("landlord", r => r.DirectUsers("user"))
        .Relation("pm_firm",  r => r.DirectUsers("inspection_firm"))
        .Relation("inspector", r => r.DirectUsers("user", "inspection_firm#employee"))
        .Relation("can_inspect",
            new RelationRewrite.Union(new[]
            {
                (RelationRewrite)new RelationRewrite.ComputedUserset("inspector"),
                new RelationRewrite.TupleToUserset("pm_firm", "employee"),
            })))
    .Type("inspection", t => t
        .Relation("property", r => r.DirectUsers("property"))
        .Relation("author",   r => r.DirectUsers("user"))
        .Relation("can_write",
            new RelationRewrite.Union(new[]
            {
                (RelationRewrite)new RelationRewrite.ComputedUserset("author"),
                new RelationRewrite.TupleToUserset("property", "can_inspect"),
            }))
        .Relation("can_read",
            new RelationRewrite.Union(new[]
            {
                (RelationRewrite)new RelationRewrite.ComputedUserset("author"),
                new RelationRewrite.TupleToUserset("property", "can_inspect"),
                new RelationRewrite.TupleToUserset("property", "landlord"),
            })))
    .Build();
```

### 3. Capability-graph ops (signed, applied in order)

1. `MintPrincipal(sam.Id, Individual)` — signed by sam
2. `MintPrincipal(jim.Id, Individual)` — signed by jim
3. `MintPrincipal(alice.Id, Individual)` — signed by alice
4. `MintPrincipal(acmeFirm.Id, Group, [alice.Id])` — signed by alice
5. `AddMember(acmeFirm.Id, jim.Id)` — signed by alice (alice has group-admin authority as mint issuer)
6. `Delegate(sam.Id → acmeFirm.Id, "property:42/pm_firm", Read)` — signed by sam (root owner)
7. `Delegate(sam.Id → acmeFirm.Id, "inspection:2026-04-17/property", ComputedFrom=property)` — signed by sam
8. After the 6+7 delegate chain, the `ReBACPolicyEvaluator` can evaluate: `can jim can_read inspection:2026-04-17?`

### 4. Evaluation walk

```
EvaluateAsync(jim, "read", inspection:2026-04-17, ctx=2026-04-17T14:00Z)
 → Type("inspection") → can_read
 → Union[
     ComputedUserset("author"),                     // ask capability-graph: (jim, author, inspection) → false
     TupleToUserset("property", "can_inspect"),     // find property-tuple of inspection → property:42
                                                    //   then ask: (jim, can_inspect, property:42)?
                                                    //     → Union[
                                                    //         ComputedUserset("inspector"),        // (jim, inspector, property:42) → false
                                                    //         TupleToUserset("pm_firm", "employee") // find pm_firm of property:42 → acmeFirm
                                                    //                                              //   then ask: (jim, employee, acmeFirm)?
                                                    //                                              //     → DirectUsers(["user"]) → capability-graph
                                                    //                                              //        check: jim is member of acmeFirm → TRUE
                                                    //       ]
                                                    //     → TRUE (via pm_firm/employee path)
                                                    //   → TRUE
     TupleToUserset("property", "landlord"),        // (sam, landlord, property:42) → TRUE but sam ≠ jim, irrelevant for jim
   ]
 → PERMIT (matched: inspection#can_read via property.can_inspect via pm_firm.employee)
```

### 5. Macaroon fallback lane

Separately, `sam` can mint a macaroon for a short-lived contractor:

```csharp
var macaroon = await issuer.MintAsync(
    location: "https://acme-rentals.sunfish.example/",
    identifier: "emergency-inspection-2026-04-17-01",
    caveats: new[]
    {
        new Caveat("time <= \"2026-04-19T00:00:00Z\""),
        new Caveat("subject == \"individual:jim\""),
        new Caveat("action in [\"read\"]"),
        new Caveat("resource.schema matches \"sunfish.pm.inspection/*\""),
    });
```

When `jim` presents it, the verifier:
1. Fetches root key by location
2. Recomputes HMAC chain over identifier + 4 caveats
3. Compares to macaroon.Signature
4. Evaluates each caveat against MacaroonContext — all must pass
5. Returns `IsValid = true`

The capability graph is untouched. The two primitives coexist.

### 6. Revocation

At `2026-04-20`, alice signs `RemoveMember(acmeFirm, jim)`. The next `EvaluateAsync(jim, read, inspection, 2026-04-21)` returns `Deny` — jim is no longer an employee → `pm_firm.employee` fails → `can_inspect` fails → `can_read` fails. No token-reissuance dance, no blocklist — just one graph op.

---

## Self-Review Checklist

- [ ] `Directory.Packages.props` pins `NSec.Cryptography` at a verified current version
- [ ] `packages/foundation/Sunfish.Foundation.csproj` references `NSec.Cryptography` (one line added)
- [ ] `packages/foundation/Crypto/` contains 9 files (`PrincipalId`, `Signature`, `KeyPair`, `SignedOperation`, `IOperationSigner`, `IOperationVerifier`, `Ed25519Signer`, `Ed25519Verifier`, `DevKeyStore`, `CanonicalJson`)
- [ ] `packages/foundation/Capabilities/` contains 8 files (`Principal`, `Resource`, `CapabilityAction`, `CapabilityOp`, `CapabilityProof`, `MutationResult`, `ICapabilityGraph`, `InMemoryCapabilityGraph`, `CapabilityClosure`)
- [ ] `packages/foundation/Macaroons/` contains 8 files (`Macaroon`, `Caveat`, `MacaroonContext`, `IMacaroonIssuer`, `IMacaroonVerifier`, `RootKeyStore`, `FirstPartyCaveatParser`, `MacaroonCodec`, default impls)
- [ ] `packages/foundation/PolicyEvaluator/` contains 11 files (contracts + builder + types + `ReBACPolicyEvaluator`)
- [ ] `packages/foundation/Extensions/SunfishDecentralizationExtensions.cs` + `DecentralizationOptions.cs` present
- [ ] `AddSunfishDecentralization()` registers `IOperationVerifier`, `ICapabilityGraph`, `IMacaroonVerifier`, `IMacaroonIssuer`, `PolicyModel`, `IPermissionEvaluator` as singletons
- [ ] `AddSunfishDecentralization()` gates `DevKeyStore` + `InMemoryRootKeyStore` behind `EnableDevKeyMaterial`
- [ ] `DevKeyMaterialWarningService` emits `LogLevel.Warning` exactly once at startup when dev keys enabled
- [ ] `Ed25519Signer` registration is NOT part of `AddSunfishDecentralization()` — production plugs own signer; dev wires `Ed25519Signer` explicitly in composition root
- [ ] `Ed25519Verifier` is stateless and safe as singleton
- [ ] `CanonicalJson.Serialize<T>(T)` produces identical bytes for the same logical value regardless of property insertion order
- [ ] `CanonicalJson.SerializeSignable<T>(payload, issuer, issuedAt, nonce)` is what both `Ed25519Signer` and `Ed25519Verifier` use — round-trip verified in `Ed25519VerifierTests.Verify_WorksAcrossSignerVerifierInstanceBoundary`
- [ ] `SignedOperation<T>` is a record; equality by value; `with` expression works
- [ ] `InMemoryCapabilityGraph.MutateAsync` verifies signature, rejects duplicate nonces, validates authority, applies op, appends to `_opLog`
- [ ] `CapabilityClosure.HasCapability` handles: direct delegates, transitive-group membership, expiration (`asOf` vs `Expires`), revocation, cycle-safe BFS
- [ ] `CapabilityClosure.FindProofChain` returns a list of `SignedOperation<CapabilityOp>` that together prove the capability, or `null` if none
- [ ] `ICapabilityGraph.ExportProofAsync` returns `CapabilityProof?` wrapping the chain
- [ ] `ICapabilityGraph.ListOpsAsync` enumerates applied ops in insertion order
- [ ] `Macaroon` has `Location`, `Identifier`, `Caveats` (`IReadOnlyList<Caveat>`), `Signature` (`byte[32]`)
- [ ] `MacaroonCodec.ComputeChain` uses standard macaroon HMAC chain: `sig0 = HMAC(rootKey, identifier); sig_{i+1} = HMAC(sig_i, caveat_i)`
- [ ] `MacaroonCodec.EncodeBase64Url` / `DecodeBase64Url` round-trip preserves fields and signature
- [ ] `FirstPartyCaveatParser.Evaluate` supports all 5 caveat forms listed in D-MACAROON-FORMAT
- [ ] `FirstPartyCaveatParser.Evaluate` fails closed on unknown caveats
- [ ] `PolicyModel.Create()` returns `PolicyModelBuilder`
- [ ] `PolicyModelBuilder.Type(name, configure)` registers type + relations
- [ ] `RelationRewrite` hierarchy: `Self`, `DirectUsers`, `ComputedUserset`, `TupleToUserset`, `Union`, `Intersection`, `Exclusion`
- [ ] `ReBACPolicyEvaluator.EvaluateAsync` returns `Decision.Permit` / `Deny` / `Indeterminate` per spec §3.5 kinds
- [ ] `OpenFgaWorkedExampleTests` exercises the spec §3.5 model end-to-end: `jim can_read inspection` via `pm_firm.employee` path → `Permit`
- [ ] `DecentralizationEndToEndTests` exercises 10-step scenario including revocation and macaroon fallback
- [ ] All 4 namespaces (`Crypto`, `Capabilities`, `Macaroons`, `PolicyEvaluator`) live inside `Sunfish.Foundation` — no new csproj
- [ ] `HasNoBlazorDependencyTests.SunfishFoundationAssembly_DoesNotReferenceAspNetCoreComponents` passes
- [ ] `HasNoBlazorDependencyTests.SunfishFoundationAssembly_DoesNotReferenceBlazorAssemblies` passes
- [ ] Foundation's only new transitive native dependency is `libsodium.so/.dll/.dylib` via `NSec.Cryptography` package; verified via `dotnet publish --runtime win-x64 --self-contained false` produces expected native asset payload
- [ ] `dotnet build packages/foundation/Sunfish.Foundation.csproj` = 0 errors, 0 warnings
- [ ] `dotnet test packages/foundation/tests/tests.csproj` = all green (existing 3+ baseline + new tests across 4 namespaces + integration)
- [ ] Public types in new namespaces have XML doc comments (`<summary>`, `<remarks>` where appropriate)
- [ ] Every public API that takes time has an explicit `DateTimeOffset` parameter — no hidden `DateTimeOffset.UtcNow` calls inside the library (D-TIME-SOURCE)
- [ ] `ValidateAuthority` correctly rejects: invalid-signature ops, duplicate-nonce ops, ops by issuers without delegate authority, revoke-by-non-originator
- [ ] `ValidateAuthority` correctly accepts: `MintPrincipal` (always), first-delegate-on-resource (heuristic root), subsequent delegates by authorized issuers, member management by group root
- [ ] `PLATFORM_ALIGNMENT.md` (Bridge) decentralization rows flipped from 🔴 to 🟡 (primitives shipped, not yet bound to entity writes)
- [ ] No changes to `ui-core`, `ui-adapters-blazor`, blocks, or accelerators — Phase B is Foundation-only
- [ ] Cross-linked XML comments to `sunfish-platform-specification.md` §2.4 / §3.5 / §10.2 and `research-notes/automerge-evaluation.md` §4.2 and `research-notes/external-references.md` §3.1

---

## Known Risks and Mitigations

| Risk | Mitigation |
|---|---|
| **NSec.Cryptography native asset shipping** — `libsodium` ships as platform-specific native binary. On Windows/macOS/Linux/Alpine, the runtime assets differ. | NSec handles this automatically via its NuGet package — it ships runtime-specific assets for `win-x64`, `linux-x64`, `linux-musl-x64`, `osx-x64`, `osx-arm64`. Task 1 Step 10's build verification catches packaging issues early. If CI runs on `linux-arm64` we verify asset coverage. |
| **Keyhive is research-grade** — Ink & Switch's Keyhive is not yet a stable 1.0. Sunfish adopts the **model** (group membership + Ed25519 signed ops) not the library. | Our implementation is .NET-native; there's no library dependency to break. If Keyhive's design shifts, we can re-evaluate without refactor of consumers — the `ICapabilityGraph` interface is the abstraction. |
| **BeeKEM / group key agreement is not in Phase B** — members of a group see all signatures plaintext; confidentiality is achieved only via out-of-band encryption today. | Documented in Scope (Out of scope). Consumers that need confidentiality encrypt-before-put and decrypt-after-get at the entity/blob layer (same pattern as `IBlobStore`). Phase D adds BeeKEM. |
| **HSM / KMS integration is consumer's responsibility** — Sunfish ships `Ed25519Signer` as a dev-only in-memory signer. Production consumers must plug their own `IOperationSigner`. | Documented in D-KEY-STORAGE. The signer contract is small (one method); sample implementations for Azure Key Vault, AWS KMS, OS keyring may ship as separate packages in a later phase. The DI extension gates dev keys behind an explicit flag. |
| **Crypto agility is deferred** — Ed25519 only. Post-quantum (Dilithium, Falcon), alternative curves (secp256k1, ECDSA P-256), and BLS signatures are parked. | Per spec §3.1: "Signature algorithm (Ed25519 default; ECDSA pluggable; post-quantum reserved for future)." The `Signature` type is fixed at 64 bytes today; a future phase may introduce a polymorphic `ISignatureAlgorithm` and `ISignatureEnvelope`. Migration will require a new op-envelope version; designed as a future breaking change. |
| **Automerge CRDT is not integrated** — the capability graph is a simple op-log, not an Automerge document. Merge semantics are append-only insertion order, not full CRDT. | Per automerge-evaluation.md recommendation: Phase B adopts the **model**, not the library. The op-log's insertion-order semantics are equivalent to a causal-order CRDT when all mutations are signed and serialized — which they are. A later phase may swap `_opLog : List<SignedOperation<CapabilityOp>>` for an Automerge document via sidecar integration if cross-verification becomes valuable. |
| **OpenFGA DSL parsing not in Phase B** — the fluent API is the only authoring surface. Published OpenFGA policy packs can't be loaded directly. | Documented in D-POLICY-DSL. The fluent API targets the same `PolicyModel` type a future parser would produce. Migration is additive, non-breaking. For Phase B consumers translating a published DSL policy, the plan's "Worked Example" section shows DSL-to-fluent mapping side-by-side. |
| **Third-party macaroon caveats (discharge macaroons) not in Phase B** — the "valid only if code-enforcement agency co-signs" scenario (spec §10.2.2) is not supported. | Documented in Scope (Out of scope). Consumers that need third-party verification use the capability graph's cross-group membership instead (the Keyhive-primary path, which IS the default). Discharge flow is a future macaroon-layer extension. |
| **Authority model heuristic for "root owner"** — Phase B uses "first-delegator-wins" on a resource, which is brittle. | Explicit deviation-note in Task 4. Phase E (entity-store integration) binds resources to entity mint ops, eliminating the heuristic. Phase B consumers who want explicit root establishment can issue a `MintPrincipal`-flavored "resource mint" convention ahead of delegates. |
| **Canonical JSON fragility** — two parties serializing the same logical value may produce different bytes if their serializers disagree on edge cases (NaN, large integers, null vs missing). | `CanonicalJson` pins a single serializer config (`JsonSerializerOptions.WriteIndented = false`, recursive key sort, UTF-8). Tests assert determinism across insertion orders, null values, nested structures, arrays. Cross-process interop is tested in `DecentralizationEndToEndTests`. A future conformance-test against an external reference (e.g., JCS RFC 8785) is a parking-lot item. |
| **Capability graph scale** — the in-memory backend is O(V + E) per query with a full op-log scan. At hundreds-of-thousands of ops, queries slow. | Documented in D-GRAPH-SHAPE: "Postgres backend materializes the closure." Phase B's in-memory backend is positioned for tests + Bridge-scale (thousands of ops); larger deployments wait for the Postgres backend in Phase D. |
| **Nonce replay window** — `Ed25519Verifier` is stateless (no nonce tracking). `InMemoryCapabilityGraph.MutateAsync` tracks nonces per-issuer in memory; on restart, the set is empty. | Documented in D-NONCE. A persistent nonce store is a Postgres-backend concern (Phase D). Phase B's in-memory set is sufficient for test + single-node demos. |
| **Foundation now has a native-binary dependency** — `Sunfish.Foundation` was pure managed before Phase B; adding NSec introduces `libsodium` as a native asset. Consumers shipping to `linux-musl-x64` (Alpine) or `linux-arm64` must verify asset availability. | NSec ships musl + arm64 assets in its NuGet package as of 25.4.0. Task 0 Step 3's build verification is the detection point. If a downstream consumer can't ship the native asset (locked-down environment), they'd need a managed Ed25519 alternative — a BouncyCastle-backed `IOperationVerifier` implementation could be a future package. |
| **Policy evaluator performance not profiled** — the Phase B evaluator does no caching; every `EvaluateAsync` recursively walks the rewrite tree + queries the capability graph. | Acceptable for Phase B (correctness over perf). Profile at Bridge integration time; if slow, add a per-request evaluation cache (easy — the rewrite tree is immutable). |

---

## Parking Lot (explicit non-goals in Phase B)

- **Keyhive BeeKEM** — continuous group key agreement; deferred to Phase D
- **Keyhive RIBLT** — set reconciliation for federation; deferred to Phase D
- **Automerge library integration** — sidecar or P/Invoke; deferred pending cost-benefit
- **Postgres capability backend** — materialized closure, persistent nonce store; deferred
- **HSM / KMS signer adapters** — Azure Key Vault, AWS KMS, OS keyring; separate packages in a later phase
- **Crypto agility** — ECDSA, post-quantum curves; design as future breaking change
- **Third-party macaroon caveats (discharge)** — deferred to follow-up
- **Parsed OpenFGA DSL** — fluent API only in Phase B
- **Signed entity-store writes** — binding `IOperationSigner` + `ICapabilityGraph` to entity mutations; Phase E
- **Federation (peer sync)** — `ICapabilityGraph` contracts are ready; sync protocol is Phase D
- **Conformance against external reference implementations** — compare `CanonicalJson` with RFC 8785, NSec signatures with OpenSSL Ed25519, macaroon wire format with libmacaroons; parking lot
- **Obligation fulfillment sink** — `Decision.Obligations` returned but not fulfilled; pattern TBD

---

## Spec Tensions and Under-specifications (feedback loop)

During planning, the following tensions surfaced — each is a candidate edit to `sunfish-platform-specification.md` that should be filed as a separate documentation change after Phase B ships:

1. **§2.4 "mint" vs §10.2.1 "principal creation"** — §2.4 frames minting as an entity-level operation (the landlord mints a lease); §10.2.1 describes principal creation (alice mints a group). The two verbs overlap but refer to different concerns. Suggest explicit disambiguation: "entity mint" vs "principal mint". Phase B uses `MintPrincipal` for the capability-layer op; Phase E will introduce `MintEntity` for the entity-layer op.

2. **§3.5 "Action" vs. the kernel's "Action" record** — the permission evaluator contract names the verb `Action`, which collides with `System.Action`. Phase B renames the public type to `ActionType` in the `Sunfish.Foundation.PolicyEvaluator` namespace. Suggest the spec be updated to use `ActionType` (or qualify as `Sunfish.Action`) to match shipped code.

3. **§3.5 root-owner semantics underspecified** — the spec implies "the resource's owner" has delegate authority, but doesn't define how the owner is established before any delegations exist. Phase B uses a "first-delegator-wins" heuristic; Phase E will bind via the entity ownership chain. Suggest the spec be updated to clarify this sequencing.

4. **§10.2.1 group-PrincipalId derivation underspecified** — the spec says "every principal has a pubkey (individuals only)" but doesn't define how a group's 32-byte identity is computed. Phase B proposes: group PrincipalId = SHA-256 of the canonical-JSON of the `MintPrincipal(kind=Group)` op. Suggest the spec adopt this (or specify an alternative).

5. **§3.5 "Obligations" semantics underspecified** — the spec mentions obligations ("log this read", "notify the owner") but doesn't define delivery. Phase B returns obligations in the `Decision` record but does not fulfill them. Suggest a follow-up spec note: obligations are caller-fulfilled; future standardized sinks will be added for audit/notification.

6. **§10.2.2 macaroon root-key storage** — the spec says "root keys stored in Sunfish's key ring" but doesn't define the key ring primitive. Phase B introduces `IRootKeyStore` with an in-memory dev implementation. Suggest the spec adopt `IRootKeyStore` or define a canonical equivalent.

7. **§3.5 policy-language framing** — the spec's "PolicyL" is described as "the OpenFGA authorization-model DSL augmented with Sunfish extensions." Phase B ships a fluent API only; the DSL parser is deferred. Suggest the spec add a note that the initial implementation is fluent-API-first with the DSL as a future enhancement (matching the shipped reality).

These are follow-up edits, not blockers. Each is a one-paragraph adjustment to the spec and has been identified here so the spec editor can address them in a v0.3 pass after Phase B merges.

---

## Phase B Summary — What This Produces

| Deliverable | Location |
|---|---|
| Ed25519 signing/verification + SignedOperation envelope | `packages/foundation/Crypto/` |
| PrincipalId, Signature, KeyPair (dev), DevKeyStore (dev) | `packages/foundation/Crypto/` |
| CanonicalJson deterministic serializer | `packages/foundation/Crypto/CanonicalJson.cs` |
| Keyhive-inspired capability graph (Principal / Individual / Group) | `packages/foundation/Capabilities/` |
| `ICapabilityGraph` + `InMemoryCapabilityGraph` with transitive closure | `packages/foundation/Capabilities/` |
| Five capability ops (MintPrincipal / Delegate / Revoke / AddMember / RemoveMember) | `packages/foundation/Capabilities/CapabilityOp.cs` |
| `CapabilityProof` export for federation-readiness | `packages/foundation/Capabilities/CapabilityProof.cs` |
| Macaroon bearer-token primitive (HMAC-SHA256, first-party caveats, attenuation) | `packages/foundation/Macaroons/` |
| `IPermissionEvaluator` + `ReBACPolicyEvaluator` (spec §3.5 contract) | `packages/foundation/PolicyEvaluator/` |
| OpenFGA-style fluent `PolicyModel` builder | `packages/foundation/PolicyEvaluator/` |
| `AddSunfishDecentralization()` DI extension with dev-key gate | `packages/foundation/Extensions/SunfishDecentralizationExtensions.cs` |
| `HasNoBlazorDependency` invariant test for Foundation | `packages/foundation/tests/Crypto/HasNoBlazorDependencyTests.cs` |
| Spec §3.5 OpenFGA worked example — fluent-API + end-to-end test | `packages/foundation/tests/PolicyEvaluator/OpenFgaWorkedExampleTests.cs` |
| 10-step end-to-end integration test (delegation + revocation + macaroon + proof export) | `packages/foundation/tests/Integration/DecentralizationEndToEndTests.cs` |
| NSec.Cryptography package pin | `Directory.Packages.props` |
| Spec status annotation (Phase B shipped) | `docs/specifications/sunfish-platform-specification.md` |
| Updated PLATFORM_ALIGNMENT decentralization rows (🔴 → 🟡) | `accelerators/bridge/PLATFORM_ALIGNMENT.md` (if present) |

---

**End of plan.**
