---
type: question
workstream-or-chapter: W#51 Phase 2 (also affects W#48, W#52, W#54, W#55 Phase 2)
last-pr: "#670 (draft, BLOCKED on this question)"
---

W#51 P2 council blocked on C1: SHA-256-derived PrincipalId from ActorId.Value
won't match DefaultPermissionResolver's assignment-lookup key (line 250 +
:648 — Holder is original ActorId string, but resolver derives
subjectActor = principal.Id.ToBase64Url() = 43-char base64url blob).

Two viable fixes; XO decision needed before W#51 P2 + W#48/52/54/55 Phase 2
can ship cohort-correct:

**Option A: introduce IActorPrincipalResolver seam in foundation-ship-common.**
Single-method `ValueTask<Principal> ResolveAsync(TenantId, ActorId, CancellationToken)`.
Hosts wire a real Ed25519-key-backed impl; in-memory test impl for fixtures.
Each cohort Phase 2 data provider takes the resolver as a non-optional dep.
Pros: zero ADR amendments; Phase 1 contracts unchanged. Cons: new substrate
seam to maintain; hosts gain a new MUST-register dependency.

**Option B: change IQuarterdeckDataProvider (and W#52/54/55 equivalents) to
take `Principal subject` instead of `ActorId actor`.**
Pros: zero new substrate; mirrors IPermissionResolver. Cons: ADR 0080 +
0081 + 0082 + 0083 amendments; breaking change to Phase 1 interfaces
already on origin/main; consumers (Phase 3a UI renderers) must construct
Principal at the call boundary.

I recommend **A** — smaller blast radius, isolates the actor→principal
mapping to one well-defined seam, doesn't touch Phase 1 contracts already
shipped. The resolver impl is host concern; tests substitute trivially.

PR #670 stays draft pending your decision. Cohort batting average:
38-of-38 (council caught the structural break before merge).
