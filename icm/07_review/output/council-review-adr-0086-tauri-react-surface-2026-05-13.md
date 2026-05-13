# Council Review — ADR 0086 Anchor Tauri-React Product Surface

**Reviewed:** 2026-05-13  
**ADR:** `docs/adrs/0086-anchor-tauri-react-product-surface.md`  
**PR:** #737 (Draft, Proposed)  
**Reviewer model:** Opus 4.7 (xhigh)

---

## Verdict

**ACCEPT WITH AMENDMENTS** — Architecture is sound and the γ posture is well-grounded in the W#60 pivot and Anchor's-fate intake, but four blocking gaps in re-evaluation criteria, license attribution, package-namespace consistency, and CP/AP-boundary specification must be closed before CO accepts. None require structural rework.

---

## Required amendments (blocking)

### A1.1 — §Phase 3 PASS re-evaluation criteria: add max-defer clock

As written, the five criteria gate the α flip but the re-evaluation gate itself has no fired-by date. This is exactly the failure mode named in Risks ("γ posture indefinite extension"). The mitigation points at a workstream that doesn't yet exist and inherits no calendar trigger.

**Fix:** add a 6th binding clause:
> *"The re-evaluation workstream MUST be opened within 30 days of W#60 Phase 3 PASS being declared, OR within 180 days of this ADR's acceptance — whichever comes first. If neither trigger has fired by the 180-day mark, XO MUST raise a `cob-question-*` to CO escalating the indefinite-γ risk."*

### A1.2 — §Consequences/Positive: add ERPNext GPLv3 API license note

The ADR claims "F/OSS license clean across the stack" but is silent on whether API-only consumption of GPLv3 ERPNext from an MIT client triggers copyleft.

**Fix:** add to the "F/OSS license clean" consequence bullet:
> *"ERPNext (GPLv3, self-hosted) is consumed via its REST API. Per FSF guidance, GPLv3-server REST consumption from an MIT client does not trigger copyleft on the client — no ERPNext code is statically or dynamically linked into the Tauri Anchor binary. If ERPNext's license ever migrates to AGPLv3 or a similar network-copyleft license, this ADR is revisit-triggered."*

### A1.3 — §Decision/Product scope: tighten CP/AP boundary definition

"refuse offline for CP-class records (financial transactions touching the GL)" leaves an ambiguous middle zone for lease-renewal reservations, signed inspection attestations, and audit log entries — all CP-class per the paper's §2.2 but not strictly "GL-affecting."

**Fix:** replace the bullet with:
> *"Offline write queue for AP-class records (notes, photos, comments, free-text fields per paper §2.2). Refused offline (returns user-visible `OfflineRefused` with a `requiresQuorum` reason code) for CP-class records, defined as: GL-affecting financial transactions; resource reservations (lease renewals, scheduled slots); signed attestations (inspections, signatures); audit log writes. The authoritative CP/AP classification per record-type is owned by the sync engine's classification table, deliverable in W#60 Phase 3 Stage 02 architecture."*

### A1.4 — §Implementation notes/File layout: commit to rename-on-α-flip

The ADR's naming convention section says "Anchor" alone = Tauri Anchor in marketing copy, implying the path should eventually be `apps/anchor/` once `accelerators/anchor/` is retired at α. The ADR doesn't commit to this.

**Fix:** add a note to the file layout section after `apps/anchor-tauri/`:
> *"**Rename-on-α-flip:** if/when the Phase 3 PASS re-evaluation triggers the α decision (retire MAUI Anchor), `apps/anchor-tauri/` SHOULD be renamed to `apps/anchor/` in the same workstream. This ADR uses `anchor-tauri/` for the γ period only to avoid collision with `accelerators/anchor/` (MAUI). The framework name in the path is intentionally temporary."*

---

## Recommended amendments (non-blocking)

1. **§Risks — add ERPNext upstream version-drift risk.** ERPNext's REST API breaks between minor versions (historically). Add: *"ERPNext API version drift — pin ERPNext minor version in self-hosting docs; run integration tests against SQLite mirror layer before recommending users upgrade ERPNext."*

2. **§Coexistence guardrails — make framework-agnostic rule enforceable.** Add: *"PRs touching `packages/foundation-*` or `packages/blocks-*` must confirm no Blazor-only or React-only contract is introduced (CI lint or PR-template checkbox). Drift detected post-merge triggers a `cob-question-*` to XO."*

3. **§Decision/Stack table — cite Tauri v2 ARM Windows 2025-Q4 stabilization claim.** The 2025-Q4 ARM Windows claim is load-bearing for Risk #1; cite the Tauri release notes or issue tracker URL.

4. **§Naming conventions — file a `pao-question-*` within 7 days of acceptance.** "TBD with PAO during book alignment" stacks two deferrals. File the naming question to PAO inbox proactively.

5. **§Boundary with MAUI Anchor — add build-tool enforcement.** Add a CI grep or ESLint rule prohibiting `accelerators/anchor/**` imports in `apps/anchor-tauri/**` and vice versa.

6. **§Alternatives considered — note inverse symmetry with ADR 0048.** ADR 0048 rejected Tauri for the platform-showcase domain; ADR 0086 selects it for the product domain. A one-paragraph note makes this legible to future readers.

---

## Risk ratings

| Risk | Rating | Rationale |
|---|---|---|
| Tauri v2 ARM Windows regressions | **MED** | FAILED trigger + PWA fallback documented. ARM Windows has production users since 2025-Q4. Bounded hardware target. Not LOW because Sunfish has no prior production data. |
| Loro production stability | **MED** | FAILED trigger → Automerge fallback documented. ADR 0028 already accepted with confidence bounds. Real but bounded. |
| Sync engine complexity | **HIGH** | Most architecturally novel area. CP/AP boundary, write-queue refusal semantics, three-party state machine (Anchor / Bridge / ERPNext) — no prior Sunfish production experience. Mitigation is a process commitment, not a technical one. Remains HIGH until Phase 3 architecture lands. |
| γ posture indefinite extension | **MED-HIGH** without A1.1; **MED** with A1.1 | Long tail: every quarter at γ doubles maintenance surface. Required amendment A1.1 provides the enforcement surface. |

---

## Unlisted risks

1. **ERPNext upstream breaking changes / version drift** (A1 recommendation covers partially).
2. **`tauri-plugin-stronghold` maturity** — IOTA-foundation library; community-maintained Tauri plugin; thin production track record vs. `tauri-plugin-sql`.
3. **React 19 + Tailwind v4 + Vite 6 stack composition risk on ARM Windows WebView2** — each is fine in isolation; the cross-stack regression surface under WebView2 has thin data.
4. **Loro ↔ ERPNext semantic mismatch** — Loro's AP/eventual-consistency semantics are at fundamental tension with ERPNext's CP/relational/transactional semantics. The ADR names the refusal rule but doesn't address broader semantic mismatch (e.g., Loro-edited note conflicting with ERPNext field validation on sync).
5. **WebView2 runtime dependency on Windows** — Tauri on Windows targets Microsoft's Edge WebView2 runtime, which Microsoft controls and can update independently.

---

## Reviewer notes

- **Architecture is sound.** Zone A supports two surfaces sharing the same slot without strain (paper never claimed one Zone A surface per product). Four-tier UI layering is preserved: both Anchors consume same `foundation-*` + `blocks-*` packages; differ only in adapter and shell tiers.
- **γ posture is the right call.** β (both as full products) compounds maintenance forever; α now (retire MAUI mid-W#59) would invalidate fresh Crew Comms work. Phased γ → α with binding re-evaluation gate is correct.
- **ADR 0048-A3 must land on `main` before ADR 0086 acceptance.** ADR 0048-A3 scopes MAUI's applicability to platform-showcase + Crew Comms, naming ADR 0086 explicitly. The two ADRs read together describe γ cleanly. Verify ordering at merge time.
- **W#60 Phase 1 PASS is confirmed.** ERPNext self-hosted with Wave history migration is in daily use (2026-05-12). The product premise is grounded.
- **Cited symbols verified.** All cited ADRs (0014, 0017, 0028, 0031, 0032, 0048, 0067) exist. Cited intakes verified. `apps/anchor-tauri/` is a planned path (Phase 3), not yet implemented — appropriate for design-stage ADR.
- **ADR 0085 overlap check needed.** Verify ADR 0085's scope does not overlap with ADR 0086 before merge (insufficient context in this review).
- **All four required amendments are mechanical** — text-only, no structural change. Recommend landing them in a single A1 amendment commit to PR #737 before CO acceptance flip.
