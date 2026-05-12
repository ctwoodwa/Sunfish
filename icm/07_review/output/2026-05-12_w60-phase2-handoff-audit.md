# W#60 Phase 2 Hand-off Audit — 2026-05-12

**Reviewer:** XO
**Hand-off audited:** `icm/_state/handoffs/w60-erpnext-react-ui-phase2-stage06-handoff.md` (348 lines, authored before Phase 1 PASS)
**Status:** Hand-off is **substantially ready** but has 4 **blocking** gaps that will surface inside the first 30 minutes of Phase 2 build. 7 additional **non-blocking** gaps worth fixing before sunfish-PM starts to avoid mid-build halts.

---

## 🔴 Blocking gaps (must fix before build)

### G1. ERPNext doctype assumptions don't match installed reality

The hand-off (Phase 1 deliverables, Phase 3 endpoints, Phase 5 endpoints) calls these doctypes:
- `Property` — **does not exist** in ERPNext v16.17.0 default install
- `Lease` — **does not exist**
- `Maintenance Visit` — exists in ERPNext but is the *equipment maintenance* doctype, not the property-maintenance ticketing the hand-off describes
- `Payment Entry`, `Journal Entry` — **exist and are correct**

What we actually built in Phase 1: properties are modeled as **Companies** (Acero/Bosco/Escola/Shirin Properties LLC) with Real Estate accounts as **Fixed Assets**; tenants/leases were tabled. So the React "properties" screen at `/api/v1/erpnext/properties` would 404 today.

**Fix options (pick one):**
- **(a) Create the doctypes in ERPNext first** — add a Phase 2 prerequisite step: CO (or sunfish-PM via Frappe app scaffold) creates `Property` + `Lease` + `Maintenance Ticket` doctypes. ~half-day of Frappe doctype admin work. Most idiomatic; gives proper resource-style URLs.
- **(b) Repoint to existing primitives** — map "Property" → Company doctype, "Lease" → Sales Order or Subscription doctype, "Maintenance" → Task doctype. Zero Frappe admin work but the React API client gets noisier (`/api/resource/Company?filters=[["is_group","=",0],["parent_company","=","Elbrus Holding LLC"]]`).
- **(c) Frappe custom app** — author a `frappe-sunfish-property` Frappe app that bundles `Property` + `Lease` + `Maintenance Ticket` doctypes. Reusable for any Sunfish self-host. Highest-value long-term, ~1 dev-week extra.

**Recommend (c)** as the right answer for the local-first product, but (a) for the first Phase 2 to keep velocity.

### G2. ERPNext base URL is wrong

Hand-off line 51: `"BaseUrl": "http://localhost:8000"`. Actual ERPNext: `http://erp.localhost:8080` (per our `HTTP_PUBLISH_PORT=8080` and `FRAPPE_SITE_NAME_HEADER=erp.localhost`). Phase 1 Phase 1 PR will fail health check on first run.

**Fix:** change default to `http://erp.localhost:8080`. Or document `HTTP_PUBLISH_PORT` as a deployment choice and have Bridge read it from env.

### G3. Multi-company scoping is unaddressed

ERPNext has 7 companies in CO's install. When the React UI calls `GET /api/v1/erpnext/properties`, which company's properties? Hand-off implies single-company; reality is 7 (RKM, Elbrus, 4 property LLCs, WFP). Every endpoint will need a `?company=` filter or implicit-context.

**Fix:** add a `company` claim to Bridge's auth token (Phase 1 deliverable), thread it through `IERPNextClient` calls as a default `filters=[["company","=",X]]` parameter. UI needs a company switcher (existing precedent: ADR 0032 multi-team Anchor workspace switching).

### G4. Bridge auth scheme is not specified

Phase 1 says `.RequireAuthorization()` but doesn't pin which scheme. Bridge supports Okta (real), MockOktaService (dev). Without specifying, sunfish-PM will pick one and we'll find out it was wrong at PR review.

**Fix:** call out **"use Bridge's existing default authentication scheme (`MockOktaService` for dev, Okta for prod) — do not add a new scheme."** Reference `accelerators/bridge/Sunfish.Bridge/Program.cs` for the wired scheme.

---

## 🟡 Non-blocking gaps (will save a round-trip if addressed up front)

### G5. .NET target version unstated

The repo is on .NET 11 preview. Bridge proxy code will reference some Bridge primitives. Phase 1 PR should explicitly say target = .NET 11 (matching the existing Bridge .csproj).

### G6. `appsettings.Development.json` seeding

Hand-off says it's gitignored but doesn't specify how sunfish-PM populates it. **Fix:** add `appsettings.Development.json.example` to repo with placeholder values; CONTRIBUTING-REACT.md tells CO to `cp` it.

### G7. Phase 2 is online-only — make it explicit

Phase 2 has zero local-first / offline behavior. That's intentional (Phase 3 adds Tauri + SQLite + Loro), but the hand-off should say so explicitly so reviewers don't expect it.

### G8. Error handling pattern

No standard for "ERPNext returns 500" / "Bridge returns 503" / "network offline." Pick one: TanStack Query's `error` boundary + a top-level `<ErrorBoundary>` showing a Sunfish-styled retry card.

### G9. CI integration

`apps/anchor-react/` needs adding to `.github/workflows/`. Phase 1 PR should include the workflow stub (lint + vitest only; no e2e in Phase 2).

### G10. Telemetry

Bridge uses Serilog. React side should at least have a `console.error` in the error boundary + a `/api/v1/telemetry/error` POST endpoint stub (implementation deferrable but the contract should exist).

### G11. Phase 2 → Phase 3 hand-off criteria

Phase 5 PASS gate covers end-state. But Phase 3 (Tauri wrap) needs `@sunfish/ui-react` to be **buildable as ESM** and `apps/anchor-react/` to **build statically** (so Tauri can wrap the static bundle). Both are implicit in Phase 5 deliverables but should be called out as Phase 3 prerequisites.

---

## ✅ What's right with the hand-off

- Clear 5-phase decomposition (1 PR each) — matches ICM Stage 06 convention
- Per-phase PASS gates and HALT conditions
- Pre-build checklist with exact commands
- Accepted-decisions table (TanStack Query, Tailwind v4, hand-authored types, etc.)
- Symbol verification already done against main (line 325–331)
- Sub-package (`@sunfish/ui-react`) extraction strategy is sound and matches ADR 0014 conventions

---

## Recommendation

**Author a Phase 2 addendum** (`icm/_state/handoffs/w60-erpnext-react-ui-phase2-stage06-addendum.md`) that:

1. Resolves G1 with option (a) or (c) — XO recommendation: (c) custom Frappe app, but defer to CO/sunfish-PM judgment
2. Fixes G2 (port 8080), G3 (multi-company), G4 (auth scheme) — surgical edits, ~10 min
3. Adds G5–G11 as a "Build conventions" section in the addendum

**Do not edit the original hand-off** — keep audit trail intact. Addendums are the canonical mechanism (precedent: W#45 P4.5 addendum, W#54 sick-bay addendum).

**Estimated addendum size:** ~120 lines.

**Time cost to XO:** 30–45 min.
**Time saved for sunfish-PM:** estimated 2–4 hours of mid-build halts + a re-spin PR.
