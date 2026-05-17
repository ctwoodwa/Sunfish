# W#60 Phase 4 — Collaboration + Integrations
## Stage 06 Build Hand-off

**Workstream:** W#60 — ERPNext Composition Pivot  
**Phase:** 4 of 5 (Accountant access + CPA read-only + Tenant portal + Bank CSV import + Stronghold)  
**Owner:** sunfish-PM (COB)  
**Gate:** Phase 3 PASS (CO Surface Pro acceptance — PR #812 merged and CO verified offline sync)  
**Estimated effort:** ~3–4 dev-weeks (~10–14h focused sessions)  
**PR count:** 4–5 PRs

---

## Context

Phase 3 ships the Tauri v2 shell (`apps/anchor-tauri/`) with SQLite offline cache + write queue + Loro CRDT for AP-class data. Auth is stubbed via `appsettings.Development.json` — Phase 4 replaces this with Tauri Stronghold.

Phase 4 adds the multi-actor collaboration layer:
- **Accountant** gets read/write access to accounting via Bridge (server-authoritative; bidirectional peer sync deferred to Phase 5).
- **CPA** gets read-only scoped Bridge access to tax-reporting data.
- **Tenants** get a magic-link browser portal (lease + payment history + messages).
- **Bank CSV import** replaces the manual ERPNext UI for transaction entry.
- **Stronghold** secures the auth token on CO's local Tauri install.

**Architecture decision (Phase 4):** Accountant uses a Bridge role account (not a peer Anchor node). Peer sync is the right long-term architecture (it's what ADR 0061 + the paper §13 are designed for), but the engineering complexity is Phase 5 scope. Phase 4 ships a working accountant experience via Bridge; Phase 5 upgrades to local-first peer sync.

---

## Pre-build checklist

1. Phase 3 PASS confirmed — `apps/anchor-tauri/` on `main`; CO has verified offline sync on Surface Pro.
2. `gh pr list --state open` — no parallel W#60 Phase 4 PRs in flight.
3. Verify `accelerators/bridge/` is on latest main (Phase 2 endpoints present: `IERPNextClient`, role-scoped middleware, `MapListingsEndpoints`, etc.).
4. Verify `packages/blocks-crew-comms/` is on main (needed for tenant portal messaging).

---

## PR 1 — Tauri Stronghold + auth hardening

**Goal:** Replace Phase 3's plain-text dev auth token with Stronghold-backed secure storage.

**Location:** `apps/anchor-tauri/src-tauri/` + `apps/anchor-tauri/src/`

**Deliverables:**
- Add `tauri-plugin-stronghold` to `Cargo.toml`
- `src/services/credentialStore.ts` — Tauri `invoke("plugin:stronghold|init_client")` wrapper + `setToken` / `getToken` / `clearToken`
- Remove `VITE_BRIDGE_TOKEN` from `appsettings.Development.json` fallback; read-only from Stronghold on launch
- First-launch UI: if no token in Stronghold, redirect to Bridge login flow (Bridge `/auth/login?redirect=tauri://localhost`)
- Logout: clear Stronghold + redirect to login
- Tests: `src/services/credentialStore.test.ts` — mock invoke; 4 unit tests (init, set, get, clear)

**Halt conditions:**
- `tauri-plugin-stronghold` requires Tauri CLI feature flag — verify `tauri.conf.json` plugin list before coding
- Bridge login redirect back to `tauri://localhost` must be in Bridge's allowed redirect URIs — add if missing

**PASS gate:** CO can launch Anchor Tauri on Surface Pro, log in via Bridge, token stored in Stronghold, next launch skips login.

---

## PR 2 — Accountant Bridge account + accounting UI

**Goal:** Accountant gets a role-scoped Bridge account with read/write on `blocks-accounting`.

**Location:** `accelerators/bridge/` + `accelerators/bridge/src/` (React)

**Deliverables (Bridge ASP.NET):**
- `Features/Accounting/AccountantRole.cs` — `IAuthorizationPolicy`: requires `financial_role` claim; allows GET + POST/PATCH on accounting endpoints; denies DELETE
- Add `MapAccountantEndpoints()` to `Program.cs`: `GET /api/v1/accounting/ledger`, `GET /api/v1/accounting/transactions`, `POST /api/v1/accounting/journal-entries`, `GET /api/v1/accounting/reconciliation`
- Each endpoint proxies to ERPNext REST API (using existing `IERPNextClient` pattern from Phase 2)
- `POST /api/v1/accounting/journal-entries` — append-only invariant enforced (no DELETE, no edit of posted entries)
- Audit emission: `AccountingJournalEntryPosted` + `AccountingReconciliationViewed` `AuditEventType` constants

**Deliverables (React):**
- `src/pages/Accounting.tsx` — P&L summary + transaction list; CO and accountant roles see this
- `src/pages/JournalEntry.tsx` — form (date, account, debit, credit, memo); accountant-only; posts to `POST /api/v1/accounting/journal-entries`
- `src/pages/Reconciliation.tsx` — transaction list with "cleared" checkbox; accountant-only; `PATCH /api/v1/accounting/reconciliation`
- `src/components/AccountingNavGuard.tsx` — wraps accountant-only pages; redirects non-accountant to dashboard
- Add Accounting to Bridge sidebar nav; gate with `useRole('accountant')`
- Tests: `Accounting.test.tsx` (6 tests: render, load, role-gate, post journal entry, clear transaction, error state)

**Halt conditions:**
- ERPNext Chart of Accounts must match what CO configured in Phase 1 — use `GET /api/resource/Account` to discover; do NOT hardcode account names
- If `IERPNextClient` doesn't support PATCH yet, add `PatchAsync` before proceeding

**PASS gate:** Accountant logs in to Bridge, views P&L, posts a journal entry, marks a transaction cleared. CO sees the journal entry.

---

## PR 3 — CPA read-only + Tenant portal

**Goal:** CPA gets year-end data access; tenants get a magic-link browser portal.

**CPA read-only (Bridge ASP.NET):**
- `Features/TaxReporting/CpaRole.cs` — `IAuthorizationPolicy`: requires `cpa_role` claim; GET-only on accounting + tax-reporting endpoints; no POST
- `GET /api/v1/tax-reporting/year-end-summary?year={year}` — P&L by property + Schedule E categories
- `GET /api/v1/tax-reporting/export` — returns CSV or JSON for accountant's tax software (fields: income, expenses by category, depreciation, interest)
- Year-end export: CPA downloads a CSV that maps to Schedule E (US) or equivalents; no local software required
- Audit emission: `TaxReportingViewed` + `TaxReportingExported`

**Tenant portal (new React app):**

Location: `apps/tenant-portal/` — standalone Vite + React + `@sunfish/ui-react`; separate build from `apps/anchor-tauri/`

- `POST /api/v1/auth/magic-link` — Bridge endpoint; creates a short-lived JWT (24h) scoped to a single `TenantId`; sends link via crew-comms (SMS/email) using `blocks-crew-comms`
- `apps/tenant-portal/src/pages/LeaseView.tsx` — lease details + payment history (read-only via `GET /api/v1/tenant/lease` scoped to JWT's `TenantId`)
- `apps/tenant-portal/src/pages/Messages.tsx` — crew-comms thread with CO; uses WebSocket or polling on `GET /api/v1/tenant/messages`
- `apps/tenant-portal/src/pages/PaymentHistory.tsx` — payment records (read-only)
- No install required — tenant opens a link in any browser
- Magic-link audit: `TenantMagicLinkIssued` + `TenantMagicLinkConsumed` (follow `VendorMagicLinkIssued` pattern from W#18 Phase 5 in `kernel-sync`)

**Tests:**
- `src/pages/TaxReporting.test.tsx`: 4 tests (render, load summary, export CSV, CPA role gate)
- `apps/tenant-portal/src/pages/LeaseView.test.tsx`: 4 tests (render, load lease, load payments, magic-link expired)

**Halt conditions:**
- Magic-link JWT must be single-use OR time-limited (24h) — implement token store in Bridge (in-memory for v1; redis/db for prod)
- `blocks-crew-comms` message delivery: if no delivery provider is configured in Integration Atlas, fall back to displaying the magic-link URL in CO's dashboard (CO copies and shares manually) — do NOT block on email/SMS integration being configured
- Do NOT expose other tenants' data via the magic-link endpoint — `TenantId` from JWT must be validated on every request

**PASS gate:** CO sends tenant a magic-link, tenant opens in browser, sees lease + payment history + can message CO. CPA opens year-end summary URL, sees P&L + exports CSV.

---

## PR 4 — Bank CSV import

**Goal:** CO can import bank transactions from a CSV file.

**Location:** `accelerators/bridge/` + `src/pages/BankImport.tsx`

**Deliverables (Bridge ASP.NET):**
- `POST /api/v1/accounting/bank-import` — accepts `multipart/form-data`; CSV file up to 5 MB; returns parsed transactions for review before posting
- CSV column mapping: date, amount, description, reference (flexible; CO maps columns once in UI; mapping persisted in `appsettings.json`)
- `POST /api/v1/accounting/bank-import/confirm` — accepts reviewed transactions; posts to ERPNext as `Journal Entry` documents via `IERPNextClient`
- Audit: `BankImportUploaded` + `BankImportConfirmed`

**Deliverables (React):**
- `src/pages/BankImport.tsx` — file upload → column mapping → preview table → confirm → success/error
- Column mapping stored in `localStorage` (persistent across sessions for same browser)
- "Duplicate detection" UI: if ERPNext already has an entry with same date+amount+reference, highlight as possible duplicate (warn, don't block)

**Tests:** `BankImport.test.tsx`: 6 tests (upload, parse, column mapping, duplicate detection, confirm, error state)

**Halt conditions:**
- ERPNext Journal Entry POST via API must be tested against CO's actual ERPNext instance before Phase 3 ships (otherwise confirm Phase 4 PR 4 is gated on CO's ERPNext test)
- Maximum CSV size: 5 MB covers ~50k transactions; if CO reports larger files, increase before confirming

**PASS gate:** CO uploads a bank CSV, reviews mapped transactions, confirms, transactions appear in ERPNext.

---

## PR 5 — Ledger flip + deployment guide + Windows arch-detection download page

**Location:** `README.md` + `docker-compose.prod.yml` + ICM ledger + download page

**Deliverables:**
- `docker-compose.prod.yml` — ERPNext + MariaDB + Redis + Sunfish Bridge (ASP.NET) + Nextcloud; persistent volumes; environment variable placeholders
- `README.md` section "Self-hosting" — `docker-compose up -d`; domain/TLS config notes; ERPNext first-run setup; estimated time: 20 minutes
- **Windows installer arch-detection page** (XO ruling 2026-05-16 T15-46Z; ADR 0088): a `/download` or `/install` page in `apps/docs/` (or Bridge static) that uses `navigator.userAgentData.getHighEntropyValues(['architecture'])` (UA-string fallback) to surface the correct download link (`_x64-setup.exe` vs `arm64-setup.exe`); manual arch toggle included. VS Code / Discord / Slack pattern. Multi-arch MSIX/WiX (Approach B) deferred until Windows-ARM test device on tailnet; bootstrapper `.exe` (Approach A) skipped permanently. Note the Anchor Tauri x64 + ARM64 artifacts built by dev-win; link both from the page.
- `icm/_state/workstreams/W60-*.md` — flip `status: "building"` → `status: "built"` for Phase 4; run `render-ledger.py`

**PASS gate:** New landlord following `README` can have ERPNext + Bridge running in under 20 minutes. Windows download page shows correct installer for host CPU without user needing to know their architecture.

---

## Phase 4 overall PASS criteria

- Accountant logs into Bridge, views CO's P&L, posts a journal entry, marks a transaction cleared
- CPA views year-end summary and exports CSV — no install required
- Tenant opens magic-link in browser, sees their lease + payment history + can message CO
- CO uploads bank CSV, reviews, confirms → transactions in ERPNext
- Self-hosted deployment guide covers ERPNext + Bridge in one `docker-compose up`

## Phase 4 FAIL triggers (→ fallback)

| Failure | Fallback |
|---|---|
| ERPNext API lacks a needed endpoint | Use ERPNext Frappe UI for that workflow; document the gap; move to Phase 5 |
| Magic-link delivery requires integration not yet configured | CO copies link from dashboard; portal still works |
| Bank CSV ERPNext POST rate-limited or fails | Manual import via ERPNext Frappe UI; mark as Phase 5 improvement |
| Phase 4 exceeds 4 calendar weeks elapsed | Halt; flag to XO; scope-cut to accountant + tenant portal only |

---

## What COB must NOT do

- Do NOT implement Headscale peer sync between accountant and CO in Phase 4 — this is Phase 5.
- Do NOT add a new `accelerators/anchor-tauri/` feature in this phase — Tauri work is Phase 3 only.
- Do NOT change the ERPNext data model — all writes go through `IERPNextClient` which calls ERPNext REST API.
- Do NOT use client-side rendering for the tenant portal's lease/payment data (PII). Server-side data fetching with JWT validation on every request.

---

## References

- **Phase 3 hand-off** — `icm/_state/handoffs/w60-tauri-offline-phase3-stage06-handoff.md` (stronghold stub, CP/AP boundary)
- **ADR 0061** — `docs/adrs/0061-headscale-mesh-vpn.md` (accountant peer sync design — Phase 5)
- **ADR 0086** — `docs/adrs/0086-anchor-tauri-react-product-surface.md` (Tauri shell)
- **ADR 0088** — `docs/adrs/0088-multiarch-windows-installer-packaging.md` (PR 5 download page; C-now / B-deferred / A-skip)
- **W#18 magic-link precedent** — `AuditEventType.VendorMagicLinkIssued` in `Sunfish.Kernel.Audit`
- **blocks-crew-comms** — `packages/blocks-crew-comms/` (tenant portal messaging)
- **IERPNextClient** — `accelerators/bridge/Sunfish.Bridge/Proxy/IERPNextClient.cs` (Phase 2)
- **UPF plan Phase 4** — `~/.claude/plans/noble-crunching-hopper.md` §Phase 4
