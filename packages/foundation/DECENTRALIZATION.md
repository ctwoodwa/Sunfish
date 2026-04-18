# Sunfish Foundation — Decentralization Primitives

Four namespaces in `Sunfish.Foundation` implement Sunfish's decentralization primitives. All ship in the single `Sunfish.Foundation` assembly. **No Blazor or AspNetCore dependency** — the `HasNoBlazorDependency` invariant is test-locked.

| Namespace | Role | Spec |
|---|---|---|
| `Sunfish.Foundation.Crypto` | Ed25519 signer/verifier, `SignedOperation<T>` envelope, canonical JSON | §2.4, §10.2.1 |
| `Sunfish.Foundation.Capabilities` | Keyhive-inspired capability graph (principals, groups, delegates) | §10.2.1 |
| `Sunfish.Foundation.Macaroons` | HMAC-SHA256 bearer tokens, first-party caveats, attenuation | §10.2.2 |
| `Sunfish.Foundation.PolicyEvaluator` | OpenFGA-style ReBAC over the capability graph | §3.5 |

See `docs/specifications/sunfish-platform-specification.md` for the spec and `docs/specifications/research-notes/automerge-evaluation.md` (Keyhive) + `external-references.md` §3.1 (OpenFGA) for the research basis.

## What the primitives compose

Sunfish's decentralization story has three interlocking parts:

1. **Identity and signatures** — principals are 32-byte Ed25519 public keys (`PrincipalId`). Every mutation is a `SignedOperation<T>`: a CLR payload plus issuer, timestamp, nonce, and Ed25519 signature over canonical JSON. Stateless verification (no key storage on the verifier side).

2. **Capability graph** — authority is expressed as signed delegations on a graph of individuals and groups. Queries ("can Jim read inspection?") compute transitive closure over group membership with `asOf` time-travel and expiration. Revocation is a single `RemoveMember` op.

3. **Policy evaluation** — ReBAC models are authored with a fluent C# API that mirrors OpenFGA's authorization-model DSL. The evaluator consumes relation tuples from `IRelationTupleStore` (alongside the capability graph) and returns a `Decision` of `Permit | Deny | Indeterminate` with matched policies and obligations.

Macaroons sit alongside as a **supplementary** bearer-token primitive — useful for short-lived delegations to external parties who don't need a full graph membership.

## Worked example — inspection delegation

The canonical scenario: landlord **Sam** delegates inspection rights on **property:42** to **Acme Inspection** (a firm group). **Jim** joins Acme; later Jim is removed. Capability-graph queries and policy decisions track this end-to-end.

### Minting principals and delegating authority

```csharp
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;

using var sam = KeyPair.Generate();        // landlord
using var jim = KeyPair.Generate();        // inspector
using var alice = KeyPair.Generate();      // acme admin
using var acmeFirmKey = KeyPair.Generate(); // acme-firm group identity (see note below)

var samSigner = new Ed25519Signer(sam);
var aliceSigner = new Ed25519Signer(alice);

var graph = new InMemoryCapabilityGraph(new Ed25519Verifier());
var t0 = DateTimeOffset.Parse("2026-04-10T09:00:00Z");

// Mint principals
await graph.MutateAsync(await samSigner.SignAsync(
    new MintPrincipal(sam.PrincipalId, PrincipalKind.Individual), t0, Guid.NewGuid()));
await graph.MutateAsync(await aliceSigner.SignAsync(
    new MintPrincipal(alice.PrincipalId, PrincipalKind.Individual), t0, Guid.NewGuid()));
await graph.MutateAsync(await aliceSigner.SignAsync(
    new MintPrincipal(acmeFirmKey.PrincipalId, PrincipalKind.Group, new[] { alice.PrincipalId }),
    t0, Guid.NewGuid()));

// Alice adds Jim to the firm
await graph.MutateAsync(await aliceSigner.SignAsync(
    new AddMember(acmeFirmKey.PrincipalId, jim.PrincipalId), t0, Guid.NewGuid()));

// Sam delegates can_write on inspection:2026-04-17 to the firm, expires 2026-06-30
var inspection = new Resource("inspection:2026-04-17");
await graph.MutateAsync(await samSigner.SignAsync(
    new Delegate(
        acmeFirmKey.PrincipalId,
        inspection,
        new CapabilityAction("can_write"),
        Expires: DateTimeOffset.Parse("2026-06-30T23:59:59Z")),
    t0, Guid.NewGuid()));
```

### Querying the graph directly

```csharp
// Jim can write today (transitive via firm membership)
var today = DateTimeOffset.Parse("2026-04-17T14:00:00Z");
bool canWrite = await graph.QueryAsync(
    jim.PrincipalId, inspection, new CapabilityAction("can_write"), today);
// → true

// After the expiry window
var later = DateTimeOffset.Parse("2026-07-15T00:00:00Z");
canWrite = await graph.QueryAsync(
    jim.PrincipalId, inspection, new CapabilityAction("can_write"), later);
// → false
```

### Evaluating a ReBAC policy (OpenFGA style)

```csharp
using Sunfish.Foundation.PolicyEvaluator;

var model = PolicyModel.Create()
    .Type("user")
    .Type("inspection_firm", t => t
        .Relation("employee", r => r.DirectUsers("user")))
    .Type("inspection", t => t
        .Relation("can_write", r => r.DirectUsers("user", "inspection_firm#employee")))
    .Build();

var tuples = new InMemoryRelationTupleStore();
var firm = new PolicyResource("inspection_firm", acmeFirmKey.PrincipalId.ToBase64Url());
var inspectionRes = new PolicyResource("inspection", "2026-04-17");

await tuples.AddAsync(new UsersetRef.SelfRef(firm), "can_write", inspectionRes);
await tuples.AddAsync(new UsersetRef.User(new Subject(jim.PrincipalId, [])), "employee", firm);

var evaluator = new ReBACPolicyEvaluator(model, tuples);
var decision = await evaluator.EvaluateAsync(
    new Subject(jim.PrincipalId, []),
    new ActionType("write"),
    inspectionRes,
    new ContextEnvelope(today, Purpose: "inspection-review"));
// decision.Kind == DecisionKind.Permit
// decision.MatchedPolicies == ["inspection#can_write"]
```

The capability graph and the policy evaluator are **independent**. The graph answers "does Jim have the signed capability?" using cryptographic delegation chains. The evaluator answers "does Jim satisfy the policy model's relations?" using a tuple store. Applications typically maintain both so graph mutations (like `RemoveMember`) can trigger tuple-store deltas.

### Short-lived delegation via macaroon

```csharp
using Sunfish.Foundation.Macaroons;

var keys = new InMemoryRootKeyStore();
var rootKey = new byte[32]; System.Security.Cryptography.RandomNumberGenerator.Fill(rootKey);
keys.Set("https://acme-rentals.sunfish.example/", rootKey);

var issuer = new DefaultMacaroonIssuer(keys);
var verifier = new DefaultMacaroonVerifier(keys);

var token = await issuer.MintAsync(
    "https://acme-rentals.sunfish.example/",
    "emergency-read-2026-04-17",
    new[]
    {
        new Caveat("time <= \"2026-04-19T00:00:00Z\""),
        new Caveat("subject == \"individual:jim\""),
        new Caveat("action in [\"read\"]"),
    });

var ctx = new MacaroonContext(
    Now: DateTimeOffset.Parse("2026-04-18T12:00:00Z"),
    SubjectUri: "individual:jim",
    ResourceSchema: null,
    RequestedAction: "read",
    DeviceIp: null);

var result = await verifier.VerifyAsync(token, ctx);
// result.IsValid == true
```

## DI wiring

```csharp
services.AddSunfish()
        .AddSunfishDecentralization(o =>
        {
            o.EnableDevKeyMaterial = true;   // dev/test only
            o.PolicyModel = model => model
                .Type("user")
                .Type("inspection", t => t.Relation("can_write", r => r.DirectUsers("user")));
        });
```

Resolving `IOperationVerifier`, `ICapabilityGraph`, `IPermissionEvaluator`, `IRelationTupleStore`, and (when dev keys are enabled) `IMacaroonIssuer` / `IMacaroonVerifier` / `IRootKeyStore` / `DevKeyStore` returns the registered singletons. When `EnableDevKeyMaterial` is on, a `DevKeyMaterialWarningService` emits a `LogLevel.Warning` at startup.

**Production consumers must plug their own `IOperationSigner`** (backed by Azure Key Vault / AWS KMS / PKCS#11 HSM / OS keyring). `Ed25519Signer` is a dev/test in-memory signer and should never be registered in production.

## Scope boundaries

Out of scope for the Phase B shipment:

- **Federation (peer sync)** — contracts (`ListOpsAsync`, `ExportProofAsync`) are ready; protocol is a later platform phase.
- **Signed entity writes** — binding signed ops to entity-store mutations is a follow-up.
- **Postgres capability backend** — in-memory only; materialized closure is a deployment-scale concern.
- **BeeKEM / RIBLT** — group key agreement and set reconciliation are deep research tracks.
- **Third-party macaroon caveats (discharge macaroons)** — first-party only today.
- **Parsed OpenFGA DSL** — fluent API only; a parser would target the same `PolicyModel` type.
- **Crypto agility / post-quantum** — Ed25519 only; future `ISignatureAlgorithm` abstraction is parked.
- **Automerge CRDT library integration** — Sunfish adopts the Keyhive **model**, not the library.

The primitives are designed so these deferrals don't force API rework: `ICapabilityGraph`, `IPermissionEvaluator`, and `IRelationTupleStore` are the extension seams.

## Tests

The worked example above is exercised end-to-end by:

- `packages/foundation/tests/PolicyEvaluator/OpenFgaWorkedExampleTests.cs` — spec §3.5 fluent policy model, Jim → employee → firm → pm_firm → property:42 → can_inspect → inspection chain.
- `packages/foundation/tests/Integration/DecentralizationEndToEndTests.cs` — 6-step property-management scenario with expiration + RemoveMember revocation + macaroon fallback + proof export.

206 passing tests across Crypto, Capabilities, Macaroons, PolicyEvaluator, Extensions, and Integration as of the Phase B shipment.
