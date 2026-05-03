# Sunfish.Kernel.Buckets

Declarative sync-bucket engine for the Sunfish local-node architecture.

This package implements [paper](../../_shared/product/local-node-architecture-paper.md)
**§10 Partial and Selective Sync** — §10.2 declarative YAML buckets and §10.3 lazy-fetch
stubs with storage-budget LRU eviction.

Wave 2.4 of [paper-alignment-plan.md](../../_shared/product/paper-alignment-plan.md).

## Why buckets?

Full replication to every node becomes a storage problem and a security problem (paper §10.1).
Sync **buckets** are named, declaratively specified subsets of the team dataset, each associated
with a role attestation. Eligibility is evaluated at capability negotiation; non-eligible nodes
never receive bucket events.

## YAML format (paper §10.2)

```yaml
buckets:
  - name: team_core
    record_types: [projects, tasks, members, comments]
    filter: record.team_id = peer.team_id
    replication: eager
    required_attestation: team_member

  - name: archived_projects
    record_types: [projects, tasks]
    filter: project.archived = true
    replication: lazy
    required_attestation: team_member
    max_local_age_days: 90
```

## Eligibility flow

1. Operator loads bucket definitions from YAML via `IBucketYamlLoader.LoadFromFile(path)`.
2. Each `BucketDefinition` is `Register`ed into `IBucketRegistry`.
3. On peer handshake (sync-daemon `CAPABILITY_NEG`), the remote peer sends an
   `AttestationBundle` of `RoleAttestation` records. Caller verifies them via
   `IAttestationVerifier`.
4. Caller passes verified attestations to `IBucketRegistry.EligibleBucketsFor(...)`, which
   returns the subset of buckets whose `RequiredAttestation` matches.
5. The sync daemon then subscribes the peer to exactly those buckets (paper §11.2 Layer 3
   "stream-level data minimization").
6. Per-record filtering inside an eligible bucket uses `IBucketFilterEvaluator`
   (`SimpleBucketFilterEvaluator` default, minimal `field (=|!=) literal (AND …)*` grammar).

## Lazy fetch and storage budgets (paper §10.3)

- Lazy-replicated records are materialised locally as `BucketStub` records (id, metadata,
  content hash, length).
- `IStorageBudgetManager` tracks the total bytes currently consumed by full-content records;
  near-limit triggers `EvictLruAsync`, which removes content from the least-recently-accessed
  records in lazy buckets while retaining their stubs.
- Default budget: 10 GB (paper §10.3). Override via `StorageBudget.MaxBytes`.
- Real fetch-from-peer is out of scope for this wave (integrates with gossip in a later wave).

## Dependencies

- **Sunfish.Foundation** — shared types.
- **Sunfish.UICore** — kernel-facade cross-link.
- **Sunfish.Kernel** — kernel-facade cross-link.
- **Sunfish.Kernel.EventBus** — `IEventLog` is the substrate buckets filter over.
- **Sunfish.Kernel.Security** — `RoleAttestation` / `IAttestationVerifier` drive eligibility.
- **YamlDotNet 16.3.0** — YAML deserialisation.

## Registration

```csharp
services.AddSunfishKernelBuckets();

// Then, at startup:
var loader = provider.GetRequiredService<IBucketYamlLoader>();
var registry = provider.GetRequiredService<IBucketRegistry>();
foreach (var def in loader.LoadFromFile("./config/buckets.yaml"))
{
    registry.Register(def);
}
```

## Filter-evaluator limitations

The shipped `SimpleBucketFilterEvaluator` is minimal:

- Operators: `=`, `!=`, `AND` only. No OR, NOT, parentheses, `<`, `>`, `IN`, `LIKE`.
- No arithmetic, no function calls, no date arithmetic.
- Identifiers are ASCII alphanumeric + underscore + dot.
- Right-hand side may be a literal (number, `true`/`false`/`null`, single-/double-quoted string) or a dotted field reference (`peer.team_id`) resolved from the context bag. This covers the paper's one cross-field example `record.team_id = peer.team_id`.
- Missing context fields resolve to `null`; equality against any non-null literal returns `false`.
- Throws `BucketFilterSyntaxException` on any unknown syntax. No arbitrary user-input code
  execution — the tokenizer and parser are fixed.

Paper §10 does not dictate an expression language. This minimal grammar covers the paper's
own examples and is deliberately small so a richer engine (Jint, Sprache, or a hand-rolled
full expression tree) can swap in during a later wave without breaking callers.

## Related

- Paper §10.1 — Why full replication fails.
- Paper §11.2 Layer 3 — Stream-level data minimization.
- Paper §11.3 — Role attestation vs. key distribution (upstream of eligibility).
- Wave 1.6 — `packages/kernel-security` (attestations).
- Wave 2.1 — `packages/kernel-sync` (gossip transport that consumes bucket subscriptions).
