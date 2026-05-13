# Discovery — W#60 Final-Stack F/OSS Substitutability Re-check

**Date:** 2026-05-13
**Author:** XO
**Workstream:** W#60 (input to Phase 5 — Self-Hosting Docs + F/OSS Polish)
**Pipeline variant:** `sunfish-feature-change` (discovery output feeding Phase 5 deliverable)
**Predecessor:** 2026-05-11 F/OSS gap/conflict analysis (memory: `project_foss_gap_conflict_analysis_2026_05_11.md`)

---

## Why this re-check

The 2026-05-11 F/OSS analysis was authored **before** W#60 Phase 1 PASS (2026-05-12) and **before** the W#60 stack picks were finalized (ADR 0086 + ADR 0048 A3, in flight). Since then:

- ERPNext (GPLv3) entered the stack as the canonical property/accounting engine
- Tauri v2 + Loro CRDT firmed up as the desktop + AP-class CRDT picks (ADR 0086)
- The substitutability principle (`feedback_oss_substitutability_principle.md` — ADR 0067-A1 Tailscale → Headscale precedent) becomes a Phase 5 deliverable

This document applies the May 11 analysis to W#60's now-concrete stack and produces the **substitutability matrix** that Phase 5 PASS criterion #2 requires (`_shared/operations/LICENSES.md`).

---

## Stack inventory (W#60 final)

| Component | Role | License | Status in stack |
|---|---|---|---|
| **ERPNext** (`frappe/erpnext`) | Property + accounting engine | GPLv3 | Phase 1 PASS — in daily use |
| **Frappe Framework** | ERPNext's underlying app framework | MIT | Inherited via ERPNext |
| **MariaDB** | ERPNext's primary RDBMS | GPLv2 + FOSS exception | ERPNext default; Postgres alternative supported |
| **Redis** | ERPNext cache + queue | BSD-3 | (Note: Redis 7.4+ moved to SSPL/RSALv2; we use the older permissive-licensed line or Valkey) |
| **Bridge** (`accelerators/bridge/`) | ASP.NET Core proxy + relay | MIT (Sunfish-authored) | Phase 2 Phase 1 in flight (PR #731) |
| **Tauri v2** | Native desktop shell | Apache-2.0 OR MIT (dual) | Phase 3 deliverable per ADR 0086 |
| **React 19** | UI framework | MIT | Phase 2 deliverable |
| **Vite 6** | Build tooling | MIT | Phase 2 deliverable |
| **Tailwind v4** | Styling | MIT | Phase 2 deliverable |
| **shadcn/ui** | Component patterns | MIT (effectively — code-into-your-repo model) | Phase 2 deliverable |
| **TanStack Query v5** | Server-state caching | MIT | Phase 2 deliverable |
| **Zustand 5** | Local state | MIT | Phase 2 deliverable |
| **React Hook Form + Zod** | Forms + validation | MIT | Phase 2 deliverable |
| **Loro CRDT** | AP-class CRDT | MIT | Phase 3 deliverable per ADR 0028 |
| **`tauri-plugin-sql`** (SQLite) | Local persistence | Apache-2.0 OR MIT | Phase 3 deliverable |
| **`tauri-plugin-stronghold`** | Keychain storage | Apache-2.0 OR MIT | Phase 3 deliverable |
| **Headscale** | Tier 2 mesh control plane | BSD-3 | Phase 4 deliverable (per ADR 0067-A1) |
| **Tailscale agent** | Tier 2 mesh client | BSD-3 (open-source agent; MIT for Go client) | Phase 4 |
| **Sunfish-authored code** | Everything else | TBD (Phase 5 D4 — recommend MIT) | Repo |

---

## License-risk classes

### Class A — clean permissive (MIT / Apache-2.0 / BSD-3)

Frappe, Bridge (Sunfish), Tauri v2, React, Vite, Tailwind, shadcn/ui, TanStack Query, Zustand, React Hook Form, Zod, Loro, tauri-plugin-sql, tauri-plugin-stronghold, Headscale, Tailscale agent.

**Action:** none. Each gets a one-paragraph entry in `LICENSES.md` per Phase 5 PASS #2.

### Class B — GPLv2/v3 with proxy boundary

**ERPNext (GPLv3)** + **MariaDB (GPLv2 with FOSS exception)**.

This is the load-bearing license question for W#60. Two stances:

1. **Network-only proxy (recommended):** Sunfish's Bridge calls ERPNext over HTTP/JSON. It does not link, embed, or include any ERPNext code. GPLv3's reach (per FSF guidance and the standard "linking" interpretation) stops at the network boundary. Sunfish's own code stays under the Sunfish-chosen license (recommend MIT per Phase 5 intake D4).
2. **Frappe app inclusion:** If Sunfish ships a custom Frappe app (`frappe-sunfish-property`, planned Phase 5 deliverable), **that app** is a Frappe-loaded module and is governed by Frappe's MIT license (Frappe Framework is MIT, distinct from ERPNext-the-app). The Frappe app's interaction with ERPNext is via the Frappe API; it does not pull GPL'd ERPNext source.

**Net effect on Sunfish-authored code:** stays MIT under either path.

**Risk:** legal counsel should confirm. The "network boundary escapes GPL" interpretation is widely accepted (LGPL-style reasoning + the GPL FAQ on plugins) but is not codified. **Phase 5 PASS criterion implicit dependency:** legal review before declaring the Sunfish repo's license file.

**Open research question OQ-1:** what's the binding GPLv3 interpretation for "Sunfish wraps ERPNext over HTTP and ships them together via docker-compose"? Distribution as a bundle changes the analysis. Need counsel.

### Class C — license-drift risk (need re-confirmation)

**Redis** moved to SSPL/RSALv2 starting Redis 7.4 (2024). The pre-7.4 line is BSD-3 and forks (Valkey, KeyDB) are open-source. Sunfish should pin to:

- Pre-7.4 Redis BSD-3, OR
- Valkey (Linux Foundation, BSD-3) — drop-in replacement

ERPNext's `frappe_docker` defaults to a specific Redis version; verify it's not on the SSPL line. Document the version pin.

**Action OQ-2:** verify the Redis version in `erpnext-local/frappe_docker` is BSD-3-licensed. If newer, pin or switch to Valkey.

### Class D — substitutable-by-design (per `feedback_oss_substitutability_principle.md`)

For every component above, document the **permissive substitute** that the Sunfish self-hoster can swap in if the upstream license drifts. Precedent: ADR 0067-A1 (Tailscale BSL → Headscale BSD-3).

---

## Per-component substitutability matrix

| Component | Drift risk | Permissive substitute | Switch cost (engineering-weeks) |
|---|---|---|---|
| ERPNext | low (GPLv3 stable for 15+ years, large community) | Fork (e.g., OpenAccounting, Akaunting if relicensed); or build domain-specific replacement | high (~12+ weeks; ERPNext is 10y of bookkeeping infra) |
| Frappe Framework | low (MIT, large community) | Build minimal Frappe-API-compatible shim over the custom Frappe app | high (only if needed; unlikely path) |
| MariaDB | medium (Oracle could change MySQL terms; MariaDB Foundation insulates) | Postgres (BSD-style) — ERPNext supports Postgres backend; switch is a 1-week migration | medium (~1 week) |
| Redis | **high** (already drifted to SSPL post-7.4) | Valkey (BSD-3, Linux Foundation) — drop-in | low (~1 day) |
| Tauri v2 | low (Apache-2.0/MIT dual, active foundation) | Electron (MIT), PWA fallback | medium (~2 weeks Tauri→Electron) |
| React | very low | Solid (MIT) or Preact (MIT) for compatibility | medium (~1–2 weeks JSX/hooks differences) |
| Vite | very low | webpack, esbuild | low (build tool swap; ~1 day) |
| Tailwind v4 | very low | UnoCSS (MIT), vanilla CSS Modules | low (~2 days config swap) |
| shadcn/ui | n/a (code-into-repo) | n/a — no upstream dependency once copied | n/a |
| TanStack Query | very low | SWR (MIT) | low (~1 day API equivalence) |
| Zustand | very low | Jotai (MIT), Valtio (MIT) | low (~half-day) |
| React Hook Form | very low | Formik (MIT), Tanstack Form (MIT) | low (~half-day) |
| Zod | very low | Yup (MIT), Valibot (MIT) | low (~half-day) |
| Loro CRDT | medium (newer; smaller community than Y.js) | Automerge (MIT, Ink&Switch — production-proven); Y.js (MIT) **but** see C1 from May 11 analysis (Loro severs the Y.js relay ecosystem) | medium (~2 weeks; CRDT semantics differ) |
| `tauri-plugin-sql` | low | better-sqlite3 (Node binding, MIT) + custom Tauri command | medium (~1 week) |
| `tauri-plugin-stronghold` | low | OS-keychain CLI wrappers (security-cmd on macOS, wincred on Windows) + custom Tauri command | medium (~1 week) |
| Headscale | low (per ADR 0067-A1 it's *already* the substitute for Tailscale BSL) | Innernet (MIT) — substantially more bare-bones; NetBird *agent* (Apache-2.0; mgmt plane BSL — see May 11 C3) | high (different mesh model; ~3 weeks) |
| Sunfish-authored | n/a (we choose) | n/a | n/a |

---

## Notable re-check findings (vs 2026-05-11 analysis)

### Re-confirmed (no change)

- **Loro over Yjs/Automerge** — still sound per ADR 0028; Loro is the right CRDT for AP-class data
- **Headscale Tier 2 mesh** — confirmed per ADR 0067-A1
- **SQLite Community Edition redistribution** for commercial tiers — same flag as May 11; pre-legal research item

### New (driven by W#60 stack picks)

- **GPLv3 boundary question (OQ-1)** is now the load-bearing legal question for the entire W#60 product. Class B above. Legal counsel before Phase 5 release.
- **Redis version pin (OQ-2)** must be verified against frappe_docker default. Low effort; do early in Phase 5.
- **Frappe custom app (`frappe-sunfish-property`)** licensing path is clean — Frappe Framework is MIT; Frappe apps are MIT-by-convention.
- **Loro production stability** at our data volumes is still under-evidenced; ADR 0086 FAILED trigger #2 captures the Automerge fallback path.

### Outdated from May 11 (de-prioritize)

- **G1 (JS sync frameworks ElectricSQL/Zero/Triplit evaluation)** — W#60 chose a different architecture (ERPNext-as-server + Tauri+SQLite client + Loro for AP). JS sync frameworks don't fit the picked architecture. **De-prioritize.**
- **C5 (AFFiNE/AppFlowy UX parity)** — W#60 is property-management-specific, not a general-purpose note-taking app. AFFiNE/AppFlowy are wrong comparison points. **De-prioritize.**
- **G4 (Cambria lens wire-format)** — W#60's schema migration is handled by ERPNext's built-in DocType + Frappe migrations, not by a custom .NET lens runtime. **Re-scope to "lens needed only if Sunfish-Frappe app schema migrations diverge from upstream ERPNext"**, which is a Phase 5+ concern.

### Still high-priority (carries over)

- **G2 Group E2E encryption (OpenMLS / Megolm)** — still needed for Crew Comms beyond pair sessions; unchanged by W#60.
- **C2 Role-key forward-secrecy** — still needs explicit ADR/paper note.
- **C3 NetBird BSL drift** — ADR 0061 amendment still pending.
- **G5 Tauri Phase 1 evaluation memo** — **now superseded** by ADR 0086 (which IS the Tauri evaluation, expressed as an ADR). Mark G5 as resolved.

---

## Recommendations for Phase 5

1. **Schedule legal counsel review** of the GPLv3 boundary question (OQ-1) before declaring the Sunfish repo's `LICENSE` file. Counsel must address:
   - HTTP-only proxy escape: does Sunfish-over-ERPNext-HTTP cross GPLv3's "derivative work" line?
   - Bundle distribution: does shipping ERPNext + Sunfish together in a docker-compose change the analysis?
   - Frappe custom app inclusion: is `frappe-sunfish-property` a Frappe-MIT artifact or an ERPNext-GPL artifact when loaded into a running ERPNext install?

2. **Verify Redis version** in `erpnext-local/frappe_docker/.env`. If on the post-7.4 SSPL line, pin to pre-7.4 or switch to Valkey before Phase 5 ships.

3. **Add to Phase 5 `LICENSES.md`:** every Class A component gets a paragraph; every Class B/C/D component gets a paragraph + a substitute name + a switch-cost estimate.

4. **Update memory file `project_foss_gap_conflict_analysis_2026_05_11.md`** to add a forward-pointer to this re-check document. The May 11 entry stays as historical record; this doc is the current state.

5. **Resolve carry-over high-priority items** in their natural phases:
   - G2 (Group E2E) — W#45 follow-on workstream
   - C2 (Role-key FS) — ADR amendment to paper §11 or whichever ADR covers role keys
   - C3 (NetBird BSL) — ADR 0061 amendment (cheap; do in Phase 5)
   - G5 (Tauri memo) — resolve as "ADR 0086 supersedes the memo requirement"

---

## Open research questions

| # | Question | Owner | Blocks |
|---|---|---|---|
| OQ-1 | GPLv3 boundary for Sunfish-over-ERPNext (HTTP proxy + docker-compose bundle + Frappe custom app inclusion) | Legal counsel + XO | Phase 5 LICENSE declaration |
| OQ-2 | Redis version in current `frappe_docker` — BSD-3 or SSPL? | XO (low effort) | Phase 5 LICENSES.md row |
| OQ-3 | If Loro proves unstable at our volumes, what's the Automerge migration story? Code-level or just CRDT type? | XO + sunfish-PM | Phase 3 FAILED-trigger response readiness |
| OQ-4 | SQLite Community Edition redistribution path for commercial accelerator tiers (carryover from May 11) | XO + Legal | Future commercial tier |

---

## Stage routing

| Stage | Action |
|---|---|
| 00 Intake | n/a — this re-check feeds the existing Phase 5 intake |
| 01 Discovery (this doc) | ✅ authored |
| 02 Architecture | when GPLv3 counsel answer lands, may produce an ADR 0087 on Sunfish public license posture |
| 06 Build | Phase 5 deliverable — `_shared/operations/LICENSES.md` consumes this matrix |

---

## Predecessors / successors

**Predecessors:**
- 2026-05-11 F/OSS gap/conflict analysis (memory)
- ADR 0067-A1 (Tailscale BSL → Headscale substitution) — precedent for substitutability principle
- W#60 Phase 1 PASS (2026-05-12)
- ADR 0086 (Tauri-React product surface, Proposed)

**Successors:**
- W#60 Phase 5 — consumes this matrix to author `_shared/operations/LICENSES.md`
- Anticipated ADR 0087 (Sunfish public license posture) — pending legal counsel answer to OQ-1
- ADR 0061 amendment (NetBird BSL drift, carryover from May 11 C3)
- Paper §11 amendment or ADR (role-key forward-secrecy explicit note, carryover from May 11 C2)
