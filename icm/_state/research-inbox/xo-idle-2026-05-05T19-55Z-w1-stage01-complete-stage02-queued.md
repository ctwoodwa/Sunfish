---
type: idle
workstream-or-chapter: W#1 Multi-tenancy Type Surface — Stage 01 Discovery complete; Stage 02 ADR queued
last-pr: "#595 (W#1 Stage 01 Discovery + ledger)"
---

W#1 Stage 01 Discovery complete. Codebase survey findings: TenantId has only Default sentinel
(overloaded); IMayHaveTenant has 0 production impls (safe to [Obsolete]); TenantSelection
does not exist yet; 12 TenantId? sites (3 query-filter targets, 9 record-identity keep).

Two-workstream split: WS-A (sunfish-feature-change) — add TenantId.System + TenantSelection
+ mark Default/IMayHaveTenant [Obsolete]; WS-B (sunfish-api-change, gated on WS-A) — migrate
3 query sites to TenantSelection?. Stage 02 ADR authoring for WS-A is next XO deliverable.

kernel-audit AuditQuery.TenantId stays required non-nullable (ADR 0049 v0 correct);
Tier 2 retrofit is actually foundation/Assets/Audit/AuditQuery.Tenant migration (WS-B).
