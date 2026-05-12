# Intake — W#60 Phase 4: Accountant Peer Node + CPA Read-Only + Tenant Portal

**Date:** 2026-05-12
**Author:** XO
**Workstream:** W#60 Phase 4 of 5
**Pipeline variant:** `sunfish-feature-change`
**Predecessor:** W#60 Phase 3 (Tauri + Loro + local-first sync engine)
**Estimate:** ~4 dev-weeks per UPF plan

---

## Problem statement

Phase 1–3 give CO a local-first single-user product. Phase 4 introduces the **multi-actor** model that the paper §15–17 calls for: three different collaborators with three very different access patterns over the same ERPNext substrate.

| Actor | Access pattern | Trust posture | Deployment |
|---|---|---|---|
| **Accountant** (Becky-the-CPA-pattern) | Read + write `blocks-accounting` + `blocks-leases`; full bidirectional sync with CO's node | Trusted peer | Peer Tauri node on accountant's laptop (Tier 2 Headscale mesh) |
| **CPA** (annual filings, audit support) | Read-only accounting + tax reporting; snapshot pulls, not continuous | Trusted but read-bounded | Bridge SaaS account OR signed snapshot export (CO chooses per relationship) |
| **Tenant** | Read own lease + own message thread; reply to messages; submit maintenance tickets; pay rent (deferred — TurboTenant remains) | Untrusted (semi-public) | Magic-link web portal on Bridge (no install, no account creation) |

Each actor's access is shaped fundamentally differently. Phase 4 establishes all three.

---

## Why now

1. **Phase 1–3 deliver a working single-user product; Phase 4 makes it a business product.** Without an accountant flow, CO does all reconciliation themselves (current state). Without a tenant portal, tenants have no view into their own ledger.
2. **Tax year 2026 close + 2027 filings approach.** CPA workflow becomes load-bearing in Q1–Q2 2027.
3. **Headscale Tier 2 mesh is already chosen** (per ADR 0067-A1 — Tailscale BSL → Headscale permissive substitute). The peer-node flow uses an existing decision rather than introducing a new one.
4. **The paper §16 (managed-relay sustainability) requires multi-actor to validate.** Single-user adoption doesn't stress the relay sustainability assumption; multi-actor does.

---

## Scope

### In scope

1. **Accountant peer node** — second Tauri app instance on accountant's hardware. Joins CO's Headscale mesh. Bidirectional sync with CO's node (and through it, with ERPNext). Role-scoped: full read/write on accounting + leases, no access to tenant communications or property internals beyond accounting context.

2. **CPA access** — CO picks one of:
   - **(a) Bridge SaaS read-only account** — CPA logs into a hosted Bridge instance; sees a read-only mirror of CO's ERPNext data. CPA never holds local data. Simpler audit story but requires the Bridge hosted instance to exist.
   - **(b) Signed snapshot export** — CO clicks "Export for CPA," receives a signed PDF + machine-readable JSON dump for a tax year. CPA receives via email/file-transfer; no live access. Matches how most small-business CPA relationships actually work.

   Recommend supporting both; (b) is default, (a) is opt-in when CPA wants live access.

3. **Tenant magic-link portal** — web app served by Bridge (NOT the Tauri Anchor). Tenant receives an email with a one-time magic link; clicks → authenticated session for 30 days; can see their own lease, payment history, message thread, and submit maintenance tickets. No password. No account.

4. **Role-claim wiring across the stack:**
   - Bridge auth issues `role` claim (`owner|accountant|cpa|tenant`) — Phase 2 stub becomes real here
   - Tauri app stores claim in keychain alongside device token
   - React UI's `<RoleGate>` (from Phase 2's `@sunfish/ui-react`) enforces UI-side access
   - Bridge proxy enforces API-side access (defense in depth) — every `/api/v1/erpnext/*` call validates role can access the requested doctype + record

5. **Tenant-side write authorization:** when a tenant submits a maintenance ticket from the portal, it routes through Bridge → ERPNext → appears in CO's Anchor maintenance queue. Tenant cannot edit existing tickets, only create new ones and comment on their own.

6. **Audit log for cross-actor edits:** every cross-actor write (accountant edits a payment CO recorded; tenant submits a ticket; CPA exports a snapshot) emits an audit entry to `kernel-audit` per ADR 0011 conventions. Visible in a "Recent activity by collaborators" widget on CO's Anchor dashboard.

### Out of scope

- Tenant rent payment via the portal — TurboTenant remains the rent collection channel (per CO 2026-05-12 conversation). Portal is read-only for payments, write-only for maintenance tickets and chat.
- Multi-property-owner support (e.g., LLC partnerships where two owners both have Anchor nodes). Treat as future workstream.
- CPA write access — out by definition.
- Tenant-to-tenant communication — out (tenants only see their own thread, not other tenants').

---

## Key design decisions to resolve in Stage 02

| # | Decision | Options |
|---|---|---|
| D1 | **Accountant peer's sync target** | (a) Accountant peer syncs with CO's ERPNext directly (via Headscale mesh + Bridge proxy on CO's node). (b) Accountant peer has its own ERPNext mirror (read-only replica). (a) is simpler; (b) avoids any availability dependency on CO's node. |
| D2 | **CPA snapshot format** | (a) PDF only (human-readable). (b) PDF + JSON (machine-readable for CPA's software). (c) PDF + JSON + CSV (Wave/QuickBooks compatibility). Recommend (b); (c) on-demand. |
| D3 | **Tenant portal hosting** | (a) Same Bridge that proxies the Tauri app. (b) Separate Bridge instance for tenant-facing surface (security isolation). Recommend (a) for MVP; isolation later. |
| D4 | **Magic-link delivery** | (a) Email via SMTP (CO's own email account). (b) Email via a Sunfish-managed relay service. (c) SMS via Twilio. Recommend (a) MVP, (b) at scale. |
| D5 | **Tenant portal UI** | (a) Reuse `apps/anchor-react/` with a tenant-mode flag. (b) Separate `apps/tenant-portal/` app (smaller bundle, tenant-only surface). Recommend (b) — bundle size matters more for public-facing surface; security isolation is clearer. |
| D6 | **Role enforcement architecture** | (a) Bridge enforces in proxy layer; UI trusts Bridge. (b) UI enforces + Bridge enforces (defense in depth). (b) is right; (a) ships faster. Recommend (b) from day one. |
| D7 | **Audit log surface** | (a) `kernel-audit` events stored locally in Anchor's SQLite + synced to ERPNext as Journal Entry comments. (b) ERPNext-only via a custom `Audit Log` doctype. (a) is local-first-aligned. |

---

## Open research questions

1. **Headscale on Surface Pro ARM** — is the Tailscale client (which Headscale uses) production-grade on ARM Windows? Last gap analysis (2026-05-11 memory) suggests yes but verify.
2. **CPA software compatibility** — what file formats do typical small-business CPAs accept? UltraTax? Drake? Lacerte? QuickBooks? Inform D2.
3. **Magic-link security model** — link rotation policy, link revocation when lease ends, single-device session vs multi-device, etc. Likely needs a security ADR.
4. **GDPR-equivalent tenant data rights** — even though VA doesn't have CCPA, tenant data deletion on lease termination is a reasonable default. Out of scope for MVP but worth flagging.
5. **Inter-company permissions at ERPNext level** — accountant has access to "all property LLC books" but maybe not Wood Family Personal. CO chooses per accountant. ERPNext User + Company Permission record maps cleanly.

---

## Acceptance criteria (Phase 4 PASS)

1. **Accountant peer node:** Set up accountant's laptop with Tauri Anchor + Headscale tailnet join. Accountant opens app, logs in, sees all 4 property LLC books. Records a reconciliation adjustment. Within 60 seconds, the edit replays to CO's ERPNext via the peer sync. CO sees the change in their Anchor (or directly in ERPNext).
2. **CPA snapshot:** CO clicks "Export FY 2026 for CPA." Receives a signed PDF + JSON bundle covering all 7 companies. Email-able. CPA receives, opens, validates.
3. **Tenant magic-link:** CO triggers "Send portal link to [tenant]" from the lease detail page. Tenant receives email, clicks link, lands in tenant portal. Sees their lease, payment history, can send a chat message + submit a maintenance ticket. Both arrive in CO's Anchor within 30 seconds.
4. **Role enforcement test:** accountant attempts to view tenant chat thread → API returns 403. Bridge proxy logs the attempt. CO can see the blocked-attempt event in the audit log.
5. **CPA cannot write:** CPA's session token rejected for any non-GET API call.

**FAILED triggers:**
- Headscale Tier 2 setup proves too brittle for accountant (non-technical user) — fall back to CPA-style snapshot mode for accountant too, drop peer node from MVP
- Magic-link email deliverability is poor (Gmail spam folder) — fall back to a Sunfish-managed relay
- ERPNext User permission model doesn't support the per-doctype + per-company + per-record granularity needed → introduce a Bridge-layer permission overlay (work item but doable)

---

## Stage routing

| Stage | Action |
|---|---|
| 00 Intake (this doc) | ✅ authored |
| 01 Discovery | needed — Headscale ARM Windows verification; CPA software survey; magic-link security model |
| 02 Architecture | needed — multiple ADRs likely: peer-sync flow, CPA snapshot format, tenant-portal trust model, role-claim wiring |
| 03 Package design | new `apps/tenant-portal/` (small React app); accountant uses existing Tauri Anchor with different role claim |
| 04 Scaffolding | re-use existing `apps/anchor-react/` for owner+accountant; new bare React scaffold for tenant-portal |
| 05 Implementation plan | sequence likely: role-claim wiring → CPA snapshot → tenant portal → accountant peer (peer last because most complex) |
| 06 Build | sunfish-PM |
| 07 Review | security council REQUIRED (multi-actor trust boundaries, magic-link, audit logging) |
| 08 Release | end-of-Phase-4 marks W#60 product feature-complete; Phase 5 is hardening + docs |

---

## Predecessors and successors

**Predecessors:**
- W#60 Phase 3 (sync engine + Tauri shell) — must be at "built"
- Phase 2's `@sunfish/ui-react` package (`<RoleGate>` component) — used here
- ADR 0067-A1 (Headscale substitution for Tailscale) — already Accepted
- ADR 0084 / 0085 (tenant-selection sentinel + query migration) — already Accepted; tenant portal's data scoping uses these
- ADR 0011 (kernel-audit) — already Accepted

**Successors:**
- W#60 Phase 5 (`docker-compose` self-hosting guide + F/OSS polish + book alignment) — final phase
- Future workstream: multi-LLC partnership support (deferred)
- Future workstream: tenant rent payment in portal (deferred, TurboTenant remains)
