# CRDT-Friendly Schema Conventions

**Status:** Canonical convention. All seven Anchor `blocks-*` clusters inherit
these rules. Stage 06 hand-offs reference this document by section.
**Date:** 2026-05-16
**Authority:** ADR 0088 — *Anchor as All-In-One Local-First Runtime* §4
(Light tier: SQLite primary + Loro CRDT sync overlay).
**Audience:** XO authoring hand-offs; cob authoring Stage 06 implementations;
dev / dev-win reading Stage 02 design docs.

---

## 0. Why this document exists

ADR 0088 ratifies Path II: Anchor is the entire stack, with **SQLite as the
primary store** and **Loro CRDT** as the peer-to-peer sync overlay between
local nodes. Five clean-room Stage 02 cluster designs landed on 2026-05-16:

| Cluster | Doc | Lines |
|---|---|---|
| `blocks-people-*` | `icm/02_architecture/blocks-people-schema-design.md` | ~1600 |
| `blocks-financial-*` | `icm/02_architecture/blocks-financial-schema-design.md` | ~2260 |
| `blocks-work-*` | `icm/02_architecture/blocks-work-schema-design.md` | ~1230 |
| `blocks-docs-*` | `icm/02_architecture/blocks-docs-schema-design.md` | ~1310 |
| `blocks-reports-*` | `icm/02_architecture/blocks-reports-schema-design.md` | ~1100 |

`blocks-people-*` was the first to explicitly design for CRDT-friendly
semantics (ULID IDs; tombstones; per-entity `version` + `revisionVector`;
stable string codes; append-only sub-collections). `blocks-financial-*`
then surfaced the hardest CRDT constraint in the cluster set: **posted
journal entries are immutable, so a Loro append-only invariant is the
strongest single demand any cluster places on the sync layer**
(see `blocks-financial-schema-design.md` §12 Q10).

This document elevates those decisions from per-cluster choices to a single
canonical convention that **all** seven clusters inherit. The goal is that
when cob (or any future implementer) reads a cluster's Stage 02 design and
hits a row like

```ts
id: ULID; createdAt; updatedAt; deletedAt?; version; revisionVector;
```

— they know what those mean, why they're shaped that way, and what
SQLite + Loro behavior is required to honor them. No cluster design has to
re-derive the convention. No two clusters can drift on it.

---

## 1. Identifier strategy — ULIDs, not autoincrement

### Rule

Every entity primary key is a **ULID** (Universally Unique Lexicographically
Sortable Identifier) — 26-character Crockford base-32, embeddable 48-bit
timestamp prefix, 80-bit random suffix.

```ts
type Id<T> = string & { readonly __brand: T };   // ULID under the hood
```

In SQLite this is a `TEXT NOT NULL PRIMARY KEY` column. No `INTEGER PRIMARY
KEY AUTOINCREMENT`. No `UUIDv4` either — sortable matters at our scale
because clustered-index locality on `(tenantId, entityType, id)` falls out
of the timestamp prefix for free.

### Why ULIDs over UUIDs

| Property | UUIDv4 | UUIDv7 | ULID |
|---|---|---|---|
| Sortable by creation time | No | Yes | Yes |
| Random suffix protects against guessing | Yes | Yes | Yes |
| Index locality (cluster-friendly) | Bad | Good | Good |
| Crockford base-32 (case-insensitive, no ambiguous chars) | No | No | Yes |
| Length on the wire | 36 | 36 | 26 |
| Round-trip-stable in JSON / TypeScript / SQLite TEXT | Yes | Yes | Yes |

UUIDv7 is acceptable as a fallback when a library only emits v7 (the
sort-by-creation property is what matters); the canonical choice is
ULID per `blocks-people-schema-design.md` §3 ("ULID — sortable, conflict-free
across nodes").

### Why not autoincrement

Autoincrement integer primary keys are forbidden because:

1. **Replicas collide.** Two Anchor nodes both write `id=42` for different
   entities. There is no recovery short of full re-keying.
2. **They leak count.** A user can infer fleet size from `Invoice.id = 1337`.
3. **They don't merge under CRDT.** Loro can't reconcile two independent
   integer streams.

### Monotonic counters — invoice numbers, JE numbers, WO numbers

This is the **single hardest** CRDT-vs-business-domain conflict in the
cluster set. Surfaced explicitly in:

- `blocks-financial-schema-design.md` §3.5 (`Invoice.invoiceNumber` —
  "INV-00123"); §3.7 (`Bill.billNumber`); §3.3 (`JournalEntry` source-ref
  references like "invoice:INV-00123"); §12 Q10 ("Loro CRDT semantics
  for posted entries").
- `blocks-work-schema-design.md` §2.4 (`WorkOrder.number` — "WO-2026-0014");
  §7 Q10 ("`WorkOrder.number` and `Contract.number` — collision-free
  generation under Loro replication?").

**The problem.** Users expect human-readable, gap-free, monotonic numbers
("INV-00123" → "INV-00124" → ...). Two offline replicas both issuing
INV-00124 cannot be reconciled by Loro alone; one of them must change.
Renumbering after the fact is unacceptable (auditors, customers, search,
URLs all break).

**The convention.** Adopt a **two-part scheme**: a **prefix** that identifies
the replica origin, and a **per-replica monotonic sequence** behind that
prefix.

```text
WO-2026-05-16-A4-0014
   └──── date ───┘ │  └─ per-replica sequence (gap-free per replica)
                   └─── 2-char replica suffix (assigned at install)
```

- **Storage:** `number TEXT NOT NULL` on the entity.
- **Generation:** local replica writes the next sequence value for its own
  prefix (e.g., `A4`). Each replica's sequence is monotonic-per-replica,
  not globally.
- **Display ordering:** when listing across replicas, sort by
  `(createdAt, replicaId, sequence)`. Display order is *not* the number
  order across replicas — but is stable.
- **Reservation:** the user picks the replica suffix at install ("Anchor
  Mac on Chris's machine" → `CW`). If two replicas were ever to share a
  suffix (laptop swap, restore from backup), the conflict is detected on
  first sync and the user is asked to re-key the smaller replica.

This pattern is documented per-cluster in:

- `blocks-financial-schema-design.md` §12 Q10 (XO recommendation —
  append-only posted entries; monotonic numbers per-replica prefixed).
- `blocks-work-schema-design.md` §7 Q10 (XO recommendation — time-prefixed
  numbers + replica suffix).

Both clusters agree. The pattern generalizes to anything else with
monotonic display numbering (purchase orders, statement numbers, receipt
numbers, etc.).

**What this convention rules out.** A global "next sequence" counter
maintained in any cluster's `*Settings` row that all replicas read-and-
increment. That pattern is forbidden under Loro because the read-then-
write race is unresolvable.

---

## 2. Soft-delete via tombstones — never `DELETE`

### Rule

Every entity carries:

```ts
deletedAt?: Date;             // null = live; non-null = tombstoned
deletedBy?: Id<Party>;        // who tombstoned
deletedReason?: string;       // optional; surfaced in admin views
```

Removing an entity sets `deletedAt = now()` + `deletedBy = currentParty.id`.
Tombstones are filtered from default queries (`WHERE deletedAt IS NULL`),
but the rows persist in SQLite + sync via Loro.

### Why tombstones, not `DELETE`

1. **Loro CRDT cannot reconcile a deletion against a concurrent edit.** If
   replica A deletes an entity while replica B edits the same entity, a
   true `DELETE` discards B's work invisibly. A tombstone preserves B's
   updates and surfaces the conflict ("this row was deleted on A but you
   edited it on B").
2. **Audit trail.** Regulatory and forensic requirements (W#37 tenant
   security policy / ADR 0068) demand "who deleted what, when." Hard
   delete loses both.
3. **Recovery.** Soft-deleted rows can be un-tombstoned (`deletedAt =
   null`); hard-deleted ones can't.
4. **Cross-cluster reference integrity.** A `Lease.tenantPartyId` that
   points at a Party row hard-deleted on another replica becomes a
   dangling pointer with no recovery. A tombstone keeps the row reachable
   for the read accessor; the UI shows "(deleted tenant)" rather than
   failing.

Documented in `blocks-people-schema-design.md` §3 ("All entities have …
`deletedAt` for tombstone-based soft-deletes; hard deletes are reserved
for compliance erasure paths only").

### When hard delete IS allowed

Two paths only:

1. **Crypto-shred / regulatory erasure** — GDPR / CCPA / right-to-be-
   forgotten. Implemented via `kernel-security` key destruction (see
   `blocks-docs-schema-design.md` §6.4); the row may remain but the
   encrypted payload becomes inert ciphertext. Hard `DELETE` is a
   degenerate case of crypto-shred for PII-bearing rows; emit a
   `RowCryptoShredded` event so other replicas reach the same state.
2. **Drafts that have never been posted / published.** A discarded
   `JournalEntry.status='Draft'` may be hard-deleted because it never
   affected balances and emitted no events
   (`blocks-financial-schema-design.md` §3.3 — "Discarding a Draft is a
   hard delete (no audit retention required for never-posted drafts in
   v1)"). Same logic applies to `Document.status='draft'`,
   `WorkOrder.status='draft'`, `Contract` drafts, etc.

Outside those two paths, hard delete is a bug.

### Cascade semantics under tombstones

When a parent is tombstoned, children are **NOT** auto-tombstoned. Reasons:

- The Loro merge story for cascaded deletes is fragile.
- Some children make sense to retain ("Tenant.deletedAt set, but the
  TenantStatement audit trail still exists in the AR ledger").
- Cascade is best handled at the read-model layer: queries filter
  `WHERE deletedAt IS NULL AND parent.deletedAt IS NULL`.

If a hand-off needs cascade behavior, model it explicitly as a
`{Entity}Tombstoned` event with handlers in dependent clusters.

---

## 3. Version vectors — `version` + `revisionVector`

### Rule

Every mutable entity carries:

```ts
version: number;                          // monotonic per-replica counter
revisionVector: Record<string, number>;   // {"A4": 17, "CW": 4} — Loro-managed
```

- `version` is a local counter, incremented on every write to the entity
  on this replica.
- `revisionVector` is the Loro version vector at the time of the last
  applied operation. Format: `{ replicaId: clock }`. Loro manages this
  field directly — application code reads it, doesn't write it.

Documented in `blocks-people-schema-design.md` §3.

### What conflicts look like

When two replicas modify the same entity concurrently, Loro's merge gives
each field its own resolution per the field-type rules in §6 below. The
**resulting `revisionVector` is the pointwise max** of the two input
vectors (this is the canonical CRDT version-vector merge rule).

When the merge result has changes the application cares about (e.g.,
state advanced from `pending` to `approved` on one side and `pending` to
`rejected` on the other), the UI is responsible for surfacing the
conflict. See §7 (state machines under CRDT) for the resolution discipline.

### How the UI is expected to handle them

| Conflict type | UI handling |
|---|---|
| Scalar field divergence (e.g., `Customer.notes`) | LWW per Loro; show "last edited by X at Y" tooltip |
| State machine divergence | Surface conflict banner; require explicit operator resolution |
| Tombstone-vs-edit | Show "(deleted on replica A — edited on this replica) — restore? confirm deletion?" |
| Append-only conflicts | None — append-only is conflict-free |

Conflict resolution UI components are owned by the consuming app
(`accelerators/anchor-react`, `accelerators/anchor`, `apps/bridge`), not
by the `blocks-*` cluster. The cluster surface exposes conflicts via a
typed conflict-iterator on each entity store.

---

## 4. Append-only sub-collections

### Rule

When an entity has a sub-collection that conceptually accumulates over
time (events, audit logs, comments, journal entries within a chart of
accounts, time entries on a work order, activities on a Party), model
the sub-collection as a **separate entity table** with append-only
semantics — never as an embedded array on the parent.

Examples from the cluster designs:

| Parent | Append-only child | Cluster |
|---|---|---|
| `Party` | `Activity` | `blocks-people-*` §3.6 ("A touchpoint with a Party — append-only by convention") |
| `JournalEntry` | `JournalLine` | `blocks-financial-*` §3.4 (lines never edited after entry posts) |
| `ChartOfAccounts` | `JournalEntry` (posted) | `blocks-financial-*` §3.3 (status=Posted is immutable) |
| `WorkOrder` | `TimeEntry` | `blocks-work-*` §2.20 |
| `Project` | `ProjectActual` | `blocks-work-*` §2.22 |
| `Document` | `DocumentRevisionHistory` | `blocks-docs-*` §3.1.3 ("Append-only journal of fine-grained edits") |
| `SigningWorkflow` | `SigningAuditLog` | `blocks-docs-*` §3.5.6 ("Append-only; one row per workflow event") |
| `Employee` | `Compensation` (historical) | `blocks-people-*` §3.2 |
| `Lead` | `Activity` (touchpoints) | `blocks-people-*` §3.6 |

### Why append-only is the right shape for CRDT

Append-only collections are **trivially conflict-free** under Loro: the
merge of two replicas' inserts is the union of the inserts. No editing of
prior rows means no last-write-wins ambiguity, no edit-vs-delete races.

Append-only collections also map naturally to event-sourcing: the rows
ARE the audit trail; no separate audit-log table is needed (cf.
`blocks-people-schema-design.md` §9 Q10 "rely on the universal event log").

### What "append-only" means in practice

1. **No `UPDATE` on these rows after insert.** Enforced by SQL trigger
   (in the persistence layer) or by application-layer write-path check.
2. **`deletedAt` IS allowed** — append-only refers to the *content*, not
   to tombstoning. A tombstoned row remains in the table; the soft-delete
   marker is the one mutation permitted.
3. **Corrections are new rows.** If an audit-log row is wrong, append a
   correcting row; never edit the original. (`blocks-financial-*` §3.3 —
   "Corrections happen via reversal + new entry.")
4. **Loro storage:** these collections use Loro's `MovableList` or
   `List` container types (kernel-crdt picks the appropriate one).
   `blocks-financial-*` §12 Q10 specifies `MovableList` for posted
   journal entries explicitly.

### What the discipline forbids

- An "events" or "audit" array embedded on the parent row (`Document.audit:
  AuditEvent[]`). Use a separate `DocumentAuditLog` table.
- Mutating an `Activity` row to "correct" its timestamp or actor —
  insert a `correction` row instead.
- A "journal lines" array on `JournalEntry` that gets edited in place
  during draft. Lines must be modeled as their own table from the start;
  draft-edits update the line rows, but post-status flip freezes them
  via the posted-then-immutable pattern (§6).

---

## 5. Stable string codes for enums

### Rule

When a field can take one of a finite set of values that the system
reasons about (status, kind, role, severity), use **stable string codes**,
not integer enums.

```ts
type WorkOrderStatusCode = 'draft' | 'planned' | 'in-progress'
                         | 'on-hold' | 'blocked' | 'completed'
                         | 'closed' | 'cancelled';
```

In SQLite: `TEXT NOT NULL` column with a CHECK constraint listing the
valid codes (or a foreign key to a code-list table if codes are
tenant-extensible).

### Why string codes, not integers

1. **Integer codes collide on CRDT merge.** If replica A adds a new code
   `8 = 'pending-review'` and replica B adds `8 = 'awaiting-approval'`,
   there is no recovery short of full migration. String codes don't
   collide: each is its own canonical value.
2. **Logs and exports are readable.** `status: 'in-progress'` survives
   round-trips through CSV, JSON, debug logs, support tickets.
3. **Schema evolution is forward-compatible.** Adding `'on-hold'` doesn't
   shift any existing code's numeric value.

Documented in `blocks-people-schema-design.md` §3 ("Mutable enum-like
fields (`Lead.status`, `Opportunity.stage`, `LeaveRequest.state`) use
stable string codes, not integers — to survive merge without integer-
collision").

### Naming codes — discipline

- **kebab-case** for multi-word codes (`'in-progress'`, `'partially-paid'`).
- **lowercase** always.
- **No spaces, no punctuation**, no internationalized characters.
- **Display labels** live separately (a `*Status` lookup table with
  `code` + `displayName` + `ordinal` + `isTerminal`, per
  `blocks-people-schema-design.md` §3.6 — `LeadStatus`).

### Deprecating codes — never reuse

When a code is retired:

1. **Do not rename it** — existing rows still carry the old string. Any
   rename is a migration that breaks merge with offline replicas.
2. **Do not remove it** from the CHECK constraint until you can prove
   no row anywhere uses it (which is hard under Loro — some offline
   replica may still re-emit it).
3. **Add a deprecation marker** in the code-list table (`isDeprecated:
   boolean`). Hide deprecated codes from new-entry pickers; show them
   on historical rows.
4. **Add a new code** for the replacement concept; don't shoehorn the
   new semantics into the old name.

Equivalent rule for the discriminated unions used across designs:
`StorageRef.kind`, `JournalEntrySource`, `DocumentType`, `WorkOrderKind`,
`ActivityKind`. Each retired value sticks around as an inactive but
recognized form forever.

---

## 6. The posted-then-immutable pattern

Several entities across the cluster set go through a state where, after a
specific transition, the entity becomes **append-only at the row level**:
no more mutations to the row itself; corrections are entirely new rows
that reference the original.

| Entity | Trigger | Immutable after | Correction mechanism |
|---|---|---|---|
| `JournalEntry` | `post()` | `status='Posted'` | Post a reversing entry (`sourceKind='Reversal'`, `reversalOf=originalId`) + a new corrected entry |
| `Invoice` | `issue()` | `status='Issued'` | `void()` posts a reversing JE; new Invoice issued for the corrected amount |
| `Bill` | `record()` | `status='Received'` (or `'Approved'` if gate enabled) | Mirror of Invoice |
| `DocumentVersion` | `publish()` | `publishedAt != null` | New version (`versionNumber + 1`); old marked `supersededAt` |
| `Contract` | counter-signing complete | `signingStatus='fully-signed'` | Amendment (`ContractAmendment` row referencing parent contract) |
| `Signature` | sign event recorded | always | None — signatures are evidence; corrections happen at the workflow level |
| `Policy` `Procedure` | `publish()` | published | New PolicyVersion + supersedes |

### Why this matters for CRDT

Loro's general merge story handles mutable rows fine (LWW per field). The
**posted-then-immutable** pattern is the special case: once a row enters
the immutable state, **no further mutation may be applied** — and Loro
must enforce that, not just trust the application code.

Concrete example from `blocks-financial-schema-design.md` §12 Q10:

> Posted journal entries are immutable. Loro's last-writer-wins for
> plain values is fine for mutable-state entities (drafts, headers), but
> immutable posted entries must be modeled as **append-only** in the
> CRDT layer (a Loro `MovableList` of entries, or per-entry CRDT
> documents with no further mutation).

### Implementation discipline

1. **Two-table option (preferred for ledger entries).** Draft entries
   live in a `journal_entry_draft` Loro container that allows mutation;
   on `post()`, the row is *atomically moved* into a separate
   `journal_entry_posted` Loro `MovableList` that the application code
   only ever appends to. Loro enforces append-only at the container
   level, not just at the application level.
2. **One-table option (acceptable for lower-stakes entities).** A single
   table with a `status` column; the persistence layer enforces "no
   `UPDATE` when status ∈ {immutable states}." Lighter weight but
   relies on the application layer (and any other replica) to honor
   the rule. Acceptable for `Document` versions, `Contract` rows,
   `Policy` versions where the audit trail is preserved separately.

The choice is per-cluster. `blocks-financial-*` chooses (1) for posted
journal entries. `blocks-docs-*` chooses (2) for DocumentVersion +
SigningAuditLog (where the audit log itself is the immutable evidence).

### Correcting an immutable row

Always via a new row that references the original:

```ts
// Reverse + correct
const reversal = JournalEntry.reverse(badEntry.id);
const correction = JournalEntry.create({ /* the right numbers */ });

// Both reversal and correction are themselves posted-then-immutable.
```

Never via an in-place edit. Two replicas attempting in-place edits would
diverge irrecoverably; two replicas posting independent reversal + new
entries converge cleanly because each operation is itself an append.

---

## 7. State machines under CRDT

### The problem

Every cluster has state machines (`Invoice` status, `WorkOrder` status,
`Project` status, `Lead` status, `Document` status, `Tenant` status,
`Lease` status, `Signature` status). State machines under Loro have a
specific failure mode: **two replicas can advance the same entity along
divergent paths**.

Surfaced explicitly in `blocks-work-schema-design.md` §7 Q7:

> Loro supports both [entity-level and field-level merges]. For
> `WorkOrder.status` transitions, last-write-wins on status conflicts
> could break the state machine invariants.

Concrete divergence example:

- Replica A: `WorkOrder.status: 'in-progress' → 'completed'`
- Replica B (offline): `WorkOrder.status: 'in-progress' → 'cancelled'`
- After sync, LWW picks one. Either way, side effects on the loser
  (closing the project on `completed`, releasing budget on `cancelled`)
  ran on a replica with a state that no longer holds.

### Three resolution patterns — pick per entity

**Pattern A — Designated authority.** One replica per entity is
designated the source of truth; other replicas can read and propose,
not advance the state. Example: a `Tenant.status` transition may only
be applied by the manager-app replica; other Anchor instances see the
transition propagate but don't advance it themselves.

- **Pro:** simplest; CRDT can use plain LWW because only one replica
  ever writes.
- **Con:** unavailable when the authority replica is offline. Surfaces a
  "pending propagation to manager" UI state.

**Pattern B — State-machine-aware merge.** Loro merge is overridden for
the status field with a custom resolver: when two transitions race, the
resolver picks the canonical winner per a per-entity rule.

Common resolver rules:

| Rule | Where applicable |
|---|---|
| Terminal states win over non-terminal | `completed` wins over `in-progress` |
| Strictly earlier in the state diagram loses | `cancelled` (terminal) wins over `on-hold` (non-terminal) |
| Both transitions are recorded; the second is a no-op | Append-only sub-collection captures both attempts |
| Operator review required | UI flag `hasPendingConflict: true`; entity locked until resolved |

`blocks-work-schema-design.md` §7 Q7 recommends "state-machine-aware
merge function for status fields; field-level LWW for everything else."
This document ratifies that recommendation as the cluster-wide default.

**Pattern C — Operator review.** When a state divergence is detected
(both transitions in the version vector with no canonical winner), the
entity is marked with `hasPendingConflict: true` and the resolution is
deferred to a human via a conflict-resolution UI. Recommended for high-
stakes states (`Lease.terminated`, `Tenant.evicted`, `Contract.cancelled`,
`Invoice.writtenOff`).

### Per-cluster guidance

| Cluster | State machine | Recommended pattern |
|---|---|---|
| `blocks-financial-*` | `Invoice` lifecycle | B with terminal-wins (`Voided` / `WrittenOff` > `Paid` > `Issued`) |
| `blocks-financial-*` | `Bill` lifecycle | Same as Invoice |
| `blocks-financial-*` | `JournalEntry` (Draft → Posted) | Posted-then-immutable (§6); no merge needed because Posted is append-only |
| `blocks-work-*` | `WorkOrder.status` | B per §7 Q7 |
| `blocks-work-*` | `Project.status` | B; `cancelled`/`closed` are terminal-wins |
| `blocks-work-*` | `Contract.status` | C (operator review) for high-stakes state |
| `blocks-docs-*` | `Document.status` | B; `archived`/`superseded` are terminal-wins |
| `blocks-docs-*` | `SigningWorkflow.status` | C (operator review); signing collisions are rare but legally significant |
| `blocks-people-*` | `Lead.statusCode` | B; `converted`/`disqualified` are terminal-wins |
| `blocks-people-*` | `Tenant.status` | C (operator review) per `blocks-people-schema-design.md` §3.5 — eviction / deceased are one-way and high-stakes |
| `blocks-people-*` | `Customer.status` | B; `blacklisted` is one-way terminal |
| `blocks-people-*` | `Opportunity.stageCode` | B per OFBiz stage-ordinal |

### Recording the chosen pattern

Each cluster's Stage 02 design doc names the pattern per state machine.
Stage 06 hand-offs reference this section + the per-cluster choice.
Implementations live in a `*StatusResolver` class per entity, registered
with kernel-crdt's conflict-resolver registry.

---

## 8. Monotonic counters — invoice numbers, JE numbers, work-order numbers

Covered in §1 above (the natural home for the discussion is identifier
strategy). Restated here for cross-reference:

- **Format:** `{kind}-{date}-{replicaId}-{sequence}` where the sequence is
  per-replica monotonic.
- **Display ordering across replicas:** sort by `(createdAt, replicaId,
  sequence)`; numbers are not globally monotonic.
- **Replica ID assignment:** at install time, persisted in
  `foundation-localfirst`'s replica record.
- **Collision detection:** on first sync, if two replicas claim the same
  ID, the smaller (by `createdAt` of the replica record) re-keys.

### What the user sees

Property managers and accountants will see invoice numbers like
`INV-2026-05-16-CW-0124`. This is uglier than `INV-00124`. Accept the
ugliness; the alternatives (renumbering after sync, gap-filled global
numbering with conflict UI) are worse.

A future ADR may revisit the display format — e.g., showing
`INV-00124 (CW)` in the UI while storing the full canonical form. The
canonical form must remain unambiguous for audit and search.

---

## 9. Large blob references vs body content — the `StorageRef` pattern

Per `blocks-docs-schema-design.md` §6, large content (PDFs, images, video,
templates) is referenced from structured rows via a discriminated union:

```ts
type StorageRef =
  | { kind: 'inline-sqlite-blob';   contentHash: string; sizeBytes: number }
  | { kind: 'fs-content-addressed'; contentHash: string; sizeBytes: number; relPath: string }
  | { kind: 'external-uri';         uri: string; sizeBytes: number | null; mimeType: string };
```

### Tier rules (from `blocks-docs-schema-design.md` §6.2)

| Body size | Tier | Where stored |
|---|---|---|
| ≤ 1 MB | `inline-sqlite-blob` | Dedicated `DocumentBlobs` SQLite table (separate from hot rows) |
| 1 MB < size ≤ 100 MB | `fs-content-addressed` | `${anchorDataDir}/blobs/${hash[0:2]}/${hash[2:4]}/${hash}` |
| > 100 MB | `external-uri` | User-configured external (S3, NAS, etc.) — not auto-synced |

### Loro sync semantics — the reference syncs; the body does not

This is the cardinal rule for CRDT sync correctness at scale:

> **Loro syncs the `StorageRef` (the reference); blob bodies sync via a
> separate content-addressed pull protocol.**

Per `blocks-docs-schema-design.md` §6.3:

- Structured rows carrying `StorageRef` flow through the Loro op-log
  unchanged.
- The body referenced by a `StorageRef` does NOT enter Loro op-log
  unless the body is `inline-sqlite-blob` (small).
- When a peer receives a Loro op referencing a previously-unseen
  `contentHash`, it issues a blob-fetch RPC against `kernel-sync`. The
  blob is content-addressed; multiple peers may serve it; first responder
  wins.
- Fetched blobs are written to the local CAS and the StorageRef
  resolves.

### Why this matters for ALL clusters, not just docs

The StorageRef pattern is the canonical attachment mechanism for **any**
cluster that handles binary content:

| Cluster | Uses StorageRef for |
|---|---|
| `blocks-docs-*` | All document bodies, marketing assets, signed PDFs, contract templates |
| `blocks-property-*` | Inspection photos, property photos (`MediaAsset`) |
| `blocks-work-*` | Maintenance task photos (`MaintenanceTask.photoMediaIds`), deliverable artifacts (`Deliverable.mediaIds`) |
| `blocks-financial-*` | Receipt scans (attached to `Receipt`), bill scans (attached to `Bill`), bank statement PDFs (`BankReconciliation` attachment) |
| `blocks-reports-*` | Rendered PDF artifacts (`ReportArtifact` — see §11 Q1 on hybrid inline/filesystem) |
| `blocks-people-*` | Resume / ID document scans (employee onboarding), background-check PDFs |

Every cluster that touches binary content references this convention via
`StorageRef` and `kernel-sync`. **No cluster invents its own blob-storage
scheme.**

### What the discipline forbids

- Embedding binary bytes inline in a structured row's column (other
  than the small-blob `inline-sqlite-blob` case in `DocumentBlobs`).
- Each cluster maintaining its own filesystem layout for attachments.
- Pushing blob bytes through the Loro op-log for entities > 1 MB —
  even one such row poisons the sync performance for all peers.

---

## 10. Validation timing — write-time vs post-merge

### The problem

Validation rules (e.g., "Invoice.amountPaid ≤ Invoice.totalAmount",
"sum(JournalLine.debit) == sum(JournalLine.credit)", "Lease.endDate >
Lease.startDate") are normally checked at write time. Under Loro, **two
replicas can each independently produce valid writes that are jointly
invalid after merge**.

Concrete example: Replica A applies a payment of $500 to Invoice X
($1000 balance). Replica B independently applies a payment of $700.
Each write is valid in isolation. After merge, the invoice shows $1200
applied against a $1000 balance — over-paid by $200.

### The convention — two-tier validation

**Tier 1: write-time validation.** Best-effort. The persistence layer
runs all entity validation rules on every insert/update. This catches
single-replica errors immediately and gives the user good feedback.

**Tier 2: post-merge validation.** Mandatory. After Loro applies an
incoming op-batch, a per-cluster reconciler runs cross-row invariants
that can't be guaranteed at write time. Categories of post-merge
invariants:

| Invariant | Cluster | Resolution |
|---|---|---|
| Sum of payment applications ≤ invoice balance | `blocks-financial-*` | Overflow flagged; over-application becomes a credit; emit `PaymentOverapplied` event |
| Sum of debits == sum of credits on posted JE | `blocks-financial-*` | Already enforced at post-time; no merge can break this because posted entries are immutable (§6) |
| Lease tenants are all live (non-tombstoned) | `blocks-property-*` × `blocks-people-*` | If a tenant was tombstoned concurrent with the lease executing on another replica, surface a "tenant deleted while activating lease" conflict |
| Contract counter-signing requires ≥ 1 non-tombstoned counterparty | `blocks-work-*` × `blocks-people-*` | Same shape; conflict surfaced |
| TrainingRequirement compliance recompute | `blocks-people-*` | Soft warning per `blocks-people-schema-design.md` §5.5 — `computeComplianceProjection` runs post-merge |
| ProjectActual rolls up time entries + work-order lines | `blocks-work-*` | Recomputed on every merge; cached in `Project.actualAmount` is denormalized and recomputed (Stage 03 may move to read-model) |

### Implementation

Each cluster registers a `IPostMergeReconciler` with kernel-crdt. The
reconciler is invoked after each batch of incoming Loro ops is applied
to the local SQLite store. Reconcilers are **idempotent** (must be safe
to run multiple times) and **non-blocking** (run async; surface results
via events or UI flags).

This pattern is documented inline in several cluster designs (most
explicitly in `blocks-financial-*` §6 algorithms and `blocks-work-*`
§4.2 budget-vs-actual reconciliation). This document elevates it to a
universal convention.

---

## 11. SQLite indexes — the canonical query shape

### The cross-replica index

Every cluster's primary entity table needs the same baseline index for
the canonical "live rows for this tenant" query:

```sql
CREATE INDEX idx_{table}_live ON {table}(tenant_id, deleted_at)
  WHERE deleted_at IS NULL;
```

This is a SQLite partial index. The query planner uses it for the
canonical UI list query:

```sql
SELECT * FROM {table}
WHERE tenant_id = ? AND deleted_at IS NULL
ORDER BY id DESC LIMIT 50;
```

(With ULID IDs, `ORDER BY id DESC` gives newest-first ordering for free
— no separate `created_at` index needed for this query.)

### Cluster-specific indexes

Each cluster's design doc enumerates its own hot query indexes. The
canonical examples:

- `blocks-financial-schema-design.md` §9 (Cluster Data Indexes — 14
  table-and-index pairs for AR, AP, GL, tax, period queries).
- `blocks-work-*` indexes (inferable from §3 cross-entity relationships;
  Stage 03 finalizes).
- `blocks-reports-*` does not maintain hot-path indexes; its data comes
  through read-only query interfaces (§9 Cross-Cluster Contracts).

### What the discipline asks

- Stage 02 design doc lists every index recommended at Stage 06.
- Stage 04 scaffolding creates the indexes as part of the migration.
- A balance-cache table (or any other denormalized roll-up) is
  introduced only when profiling on the Surface Pro 7 target shows
  > 200ms latency for the canonical UI query. (Per
  `blocks-financial-schema-design.md` §9: "Recommend not introducing
  it until query profiling shows AR/AP report latency > 200ms".)

---

## 12. Cross-entity reference integrity — orphan detection, not constraint enforcement

### The problem

SQLite supports foreign-key constraints. Loro does not — it can't
guarantee that a reference target exists, because the referent might
live on a different replica that hasn't synced yet.

Two replicas can produce:

- Replica A creates `Invoice` with `customerId = X`.
- Replica B has not yet seen `Customer` X (replica B will see it on next
  sync); the FK is briefly dangling.

If SQLite enforces the FK as `ON DELETE RESTRICT` / `ON DELETE CASCADE`,
the insert fails on Replica B until X arrives. That's broken offline-
first UX.

### The convention

1. **Disable SQLite FK enforcement.** PRAGMA `foreign_keys = OFF` in the
   Anchor SQLite connection. Reference integrity is the application
   layer's job.
2. **Read-side resilience.** Every consumer of a FK uses a *resolver* that
   returns `null` (or a typed `Unresolved<T>` sentinel) when the target
   isn't present locally. Consumers render "(unresolved {{kind}} —
   awaiting sync)" rather than throwing.
3. **Orphan detection — background pass.** A per-cluster orphan-detector
   periodically scans for references whose target has been tombstoned (or
   never arrived after extended sync time, e.g., > 7 days). Detected
   orphans surface in the admin "Sync health" view.
4. **UI: "broken link" indicators**, not fail-loud. When a referenced
   entity is tombstoned, the UI shows a strike-through name with a
   restoration link ("restore" if recoverable; "edit reference"
   otherwise).

### Why this is acceptable

The Inverted Stack paper (§13, "the local node IS the application")
treats partial state as the norm, not an exception. A user editing on
their phone while their accountant edits the same record on a tablet
spends meaningful time in partial-sync states. The system has to remain
usable through them.

Strict FK enforcement would make Anchor unusable offline. Orphan
detection plus UI flagging gives the same correctness story without the
brittleness.

### Where strict integrity DOES live

Loro itself enforces version-vector causality: an op referencing a
parent op that hasn't arrived can't be applied. So while individual
field references can be unresolved, the **CRDT graph itself** is
internally consistent. The application-level reference integrity
discussed here is a layer above that.

---

## 13. CRDT envelope — what every entity carries

Consolidating §1–§3 + §10 + §12 into a single canonical envelope, every
non-append-only entity in every cluster carries these fields:

```ts
interface CrdtEnvelope<TSelf> {
  // Identity (§1)
  id: Id<TSelf>;                       // ULID
  tenantId: Id<Tenant>;                // multi-tenant scope

  // Audit (§2, §3)
  createdAt: Date;
  createdBy: Id<Party>;
  updatedAt: Date;
  updatedBy: Id<Party>;
  deletedAt?: Date;                    // tombstone
  deletedBy?: Id<Party>;
  deletedReason?: string;

  // Version + CRDT (§3)
  version: number;                     // monotonic per-replica
  revisionVector: Record<string, number>;  // Loro-managed
}
```

For **append-only** entities (Activities, JournalEntries-post-status-Posted,
DocumentRevisionHistory, SigningAuditLog, etc.) the envelope is simpler:

```ts
interface AppendOnlyEnvelope<TSelf> {
  id: Id<TSelf>;
  tenantId: Id<Tenant>;
  createdAt: Date;
  createdBy: Id<Party>;
  // No update fields; no version vector beyond row identity;
  // tombstoning still permitted via deletedAt for crypto-shred only.
  deletedAt?: Date;
}
```

Stage 02 design docs may omit the envelope from each entity definition
for brevity (as `blocks-people-schema-design.md` §3 does explicitly).
Stage 06 implementations must materialize it on every table.

---

## 14. Multi-tenant isolation under CRDT

### The rule

Every entity carries `tenantId`. **No CRDT op may cross tenant
boundaries.** Loro replicas are configured per-tenant; the kernel-sync
transport refuses to apply an op whose tenant doesn't match the local
replica's tenant context.

This is enforced at the kernel layer (`foundation-multitenancy` per
existing project conventions; analyzer-enforced "every query has a
tenant filter") and at the sync transport.

### What this rules out

- A Party in tenant A cannot become a Party in tenant B by `tenantId`
  reassignment. Cross-tenant data movement requires an explicit export-
  and-reimport flow (Stage 06 hand-off if needed).
- A `Document.sensitivity='public'` in tenant A is not visible to
  tenant B. The Bridge tier (per ADR 0031) handles cross-tenant access
  with its own authorization story.

### Connection to W#37 tenant security policy

W#37 / ADR 0068 (Proposed) defines per-tenant key isolation. The
combination of (a) `tenantId` on every row, (b) per-tenant Loro replica
configuration, and (c) per-tenant kernel-security envelope keys gives
the full isolation story. Encryption-at-rest fields
(`Party.taxId`, `Employee.ssnEncrypted`, `Employee.bankAccountEncrypted`)
inherit tenant isolation from this rule.

---

## 15. Open questions for CO / cob ratification

These cross-cutting questions are surfaced from the five Stage 02
designs and need resolution before Stage 06 implementations begin:

### Q1. ULID library choice + replica-suffix length

**Question:** Which ULID library do we standardize on (TypeScript side
+ Rust side + C# side)? And what length is the replica suffix in the
canonical number format — 2 chars (256 replicas) or 3 chars (~16k
replicas)?

**Recommendation:** 2 chars is enough for any realistic deployment
(one small-business tenant rarely has > 10 active replicas). Library
choice: `@oslojs/crypto` ULID for TS; `ulid` crate for Rust; an in-house
.NET helper for C# (the existing Sunfish utilities namespace).

### Q2. Loro container choice per entity — `MovableList` vs `List` vs `Map`

**Question:** `blocks-financial-schema-design.md` §12 Q10 recommends
`MovableList` for posted journal entries. Other clusters' append-only
collections need the same choice ratified. Is the canonical mapping:
posted-immutable → `MovableList`; mutable rows → `Map`-per-entity; text
fields → `Text` CRDT?

**Recommendation:** Yes; document the mapping in a kernel-crdt design
ADR. Stage 03 package-design picks per-entity.

### Q3. Conflict-resolver registration mechanism

**Question:** Per §7, state-machine-aware resolvers are registered with
kernel-crdt. What's the registration shape — analyzer-enforced
attribute? DI registration? Reflection-based scan?

**Recommendation:** DI registration at cluster bootstrap, with a
companion analyzer (W#46 design-system-style) that fails the build if
a state machine field lacks a registered resolver. Stage 03 to detail.

### Q4. Post-merge reconciler scheduling — sync vs async vs batched

**Question:** §10 specifies post-merge reconcilers run after each op-
batch. Are they synchronous (block sync completion until reconciliation
finishes) or async (run in background after sync returns)?

**Recommendation:** Async. Synchronous reconciliation blocks UI on
large incoming sync batches and the offline-first promise says the user
gets to keep working. Reconciler results surface via events;
high-severity invariant failures (e.g., over-applied payments) raise
a UI flag the user must acknowledge before related actions proceed.

### Q5. Tombstone garbage collection

**Question:** Tombstones accumulate forever; for a tenant active over 10
years this becomes meaningful storage. Do we ever GC tombstones?

**Recommendation:** GC after `retentionPolicy.tombstoneRetentionDays`
(default 7 years for financial entities, 1 year for transient entities
like Lead). GC is global per-tenant; all replicas drop the tombstone
together when the threshold passes (coordinated via a system event).
A separate ADR captures the retention policy schema; out of scope here.

### Q6. Pattern A "designated authority" — how is authority assigned?

**Question:** Per §7 Pattern A, some entities have a single designated-
authority replica. Is authority a property of the entity (stored on the
row), a property of the replica (some replicas are "primary" for some
entity types), or a per-tenant configuration?

**Recommendation:** Per-tenant configuration, with a default of "any
replica can advance" (i.e., default to Pattern B). Pattern A is opt-in
for entities flagged as such in the cluster's design. Stage 03 names
the per-entity opt-in.

### Q7. Hard-delete vs crypto-shred boundary

**Question:** §2 lists two paths to hard delete: never-posted drafts +
crypto-shred. Are there other categories that should be allowed? (e.g.,
ephemeral state — typing-indicators, presence, draft autosave)

**Recommendation:** Yes; ephemeral state lives outside the audit
envelope entirely. Specifically: any entity whose lifetime is < 24h and
has no business-record value (typing indicators, presence beacons,
draft autosave shadows) lives in a separate non-CRDT scratch space,
purged on Anchor restart. Documented per-cluster in Stage 03.

### Q8. Application-layer `ON UPDATE CASCADE` equivalent

**Question:** With SQLite FK enforcement off (§12), how do we propagate
e.g. a `Customer.partyId` change to all rows that reference it?

**Recommendation:** We don't — references are stable IDs that never
change. Renaming a Party (changing `displayName`) doesn't change its
`id`. The cascade question only arises if we ever permit ID
reassignment, which the convention forbids.

### Q9. ULID timestamp drift and clock skew

**Question:** ULID encodes a 48-bit Unix-millis timestamp. If two
replicas have clocks drifted by minutes, their ULIDs are also drifted —
sorting by ID will not match wall-clock chronology. Acceptable?

**Recommendation:** Yes. ULIDs sort by *issuing replica's local clock*
which is good enough for index locality. Wall-clock chronology comes
from `createdAt`, which lives in the audit envelope and is also
replica-local. Wall-clock drift is a separate problem solved at the
foundation-localfirst level (loose time sync, not Lamport clocks).

---

## 16. Discipline summary — how to apply this document

When authoring a new entity in a cluster Stage 02 design:

1. **Use the §13 CRDT envelope.** Don't redefine ID, audit, version
   fields per entity.
2. **Pick a CRDT pattern per state machine** (§7). Document the
   resolver choice in the design doc.
3. **Identify any posted-then-immutable transitions** (§6). Name the
   correction mechanism explicitly.
4. **List append-only sub-collections** (§4) as separate entities, not
   embedded arrays.
5. **Use stable string codes** for all enums (§5). List initial codes
   plus deprecation discipline.
6. **For attachments, reference §9 `StorageRef`** — never invent a new
   blob scheme.
7. **List write-time + post-merge validations** (§10) separately.
8. **List required indexes** (§11) per entity table.
9. **List cross-cluster references** + their unresolved-handling
   strategy (§12).
10. **Note multi-tenant isolation** if anything in the entity shape
    interacts with tenancy beyond the default `tenantId` filter (§14).

Cluster Stage 06 hand-offs cite this document by section. Stage 06
implementations either honor the conventions or document a deviation
with rationale in the hand-off.

---

**End of canonical CRDT-friendly schema conventions.** Companion documents:

- `cross-cluster-event-bus-design.md` — how cross-cluster events flow
  between Anchor `blocks-*` clusters.
- `party-model-convention.md` — how the OFBiz Party + role pattern is
  shared across `blocks-people-*`, `blocks-financial-*`,
  `blocks-property-*`, `blocks-work-*`.
- `foss-source-survey-anchor-domain.md` — FOSS source survey behind
  these conventions (ADR 0088 Appendix A).
